<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { ContentRenderer, DbButton, DbChip, DbInput, mediaResolverFor } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";

const { t } = useI18n();
import type {
  Article,
  ArticleVersionSummary,
  Category,
  ContentDocument,
  TaxonomyTerm,
} from "@databro/types";

/**
 * Article editor.
 *
 * `id` of `new` means "create": the route is the same because everything except the first save is
 * identical, and splitting it would duplicate the entire form.
 */
const route = useRoute();
const router = useRouter();
const { withAuth } = useAuth();

const articleId = computed(() => String(route.params.id));
const isNew = computed(() => articleId.value === "new");

const { data: loaded, error: loadError } = await useAsyncData(
  () => `editor:${articleId.value}`,
  async () => {
    const [article, categories, tags] = await Promise.all([
      isNew.value ? Promise.resolve(null) : withAuth((api) => api.getAuthoringArticle(articleId.value)),
      withAuth((api) => api.listCategories()),
      withAuth((api) => api.listTags()),
    ]);
    return { article, categories, tags };
  },
);

if (loadError.value) throw createError({ statusCode: 404, statusMessage: t("studio.articles.notFound"), fatal: true });

const categories = computed<Category[]>(() => loaded.value?.categories ?? []);
const allTags = computed<TaxonomyTerm[]>(() => loaded.value?.tags ?? []);

// Local working copy. The form edits this; nothing reaches the API until Save.
const article = ref<Article | null>(loaded.value?.article ?? null);
const title = ref(article.value?.title ?? "");
const summary = ref(article.value?.summary ?? "");
const slug = ref(article.value?.slug ?? "");
const categoryId = ref<string>(article.value?.category?.id ?? "");
const tagIds = ref<string[]>(article.value?.tags.map((t) => t.id) ?? []);
const metaTitle = ref(article.value?.seo?.metaTitle ?? "");
const metaDescription = ref(article.value?.seo?.metaDescription ?? "");
const content = ref<ContentDocument>(article.value?.content ?? { version: 1, blocks: [] });

// The preview resolves images through the article's saved media map *plus* anything picked in this
// session, so an image just uploaded renders immediately instead of after a save-and-reload.
const { merged } = useMediaCache();
const resolveMedia = computed(() => mediaResolverFor(merged(article.value?.media)));

const saving = ref(false);
const publishing = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);

const status = computed(() => article.value?.status ?? "draft");
const isPublished = computed(() => status.value === "published");

// The slug is a public URL and immutable once published (CT-2); moving it is a separate, deliberate
// act through the slug-change endpoint, so the field locks rather than silently doing nothing.
const slugLocked = computed(() => Boolean(article.value?.publishedAt));

function describe(error: unknown) {
  if (error instanceof ApiClientError) {
    const details = Array.isArray(error.details)
      ? (error.details as Array<{ message?: string }>).map((d) => d.message).filter(Boolean).join(" ")
      : "";
    return [error.message, details].filter(Boolean).join(" — ");
  }
  return t("studio.common.genericError");
}

async function save() {
  formError.value = null;
  saving.value = true;

  try {
    const input = {
      title: title.value,
      summary: summary.value,
      content: content.value,
      // Always sent, both of them: the API treats an omitted field as "leave unchanged", so
      // clearing a category has to be an explicit null rather than an absence.
      categoryId: categoryId.value || null,
      tagIds: tagIds.value,
      seo: {
        metaTitle: metaTitle.value || undefined,
        metaDescription: metaDescription.value || undefined,
        robots: "index,follow",
      },
      ...(isNew.value && slug.value ? { slug: slug.value } : {}),
    };

    const saved = isNew.value
      ? await withAuth((api) => api.createArticle(input))
      : await withAuth((api) => api.updateArticle(articleId.value, input));

    article.value = saved;
    savedAt.value = new Date().toLocaleTimeString();

    // Move off /new so a reload does not create a second article.
    if (isNew.value) await router.replace(`/studio/articles/${saved.id}`);
  } catch (error) {
    formError.value = describe(error);
  } finally {
    saving.value = false;
  }
}

async function togglePublish() {
  formError.value = null;
  publishing.value = true;

  try {
    // Publish snapshots the *saved* draft, so an unsaved edit would silently not be published.
    await save();
    if (formError.value) return;

    const id = article.value?.id;
    if (!id) return;

    article.value = isPublished.value
      ? await withAuth((api) => api.unpublishArticle(id))
      : await withAuth((api) => api.publishArticle(id));
  } catch (error) {
    formError.value = describe(error);
  } finally {
    publishing.value = false;
  }
}

// ---- Scheduling (CT-7) ----

const scheduling = ref(false);
// `datetime-local` speaks local wall-clock with no zone; the API wants an instant. Converting at the
// boundary keeps the editor thinking in their own timezone and the API in UTC.
const scheduleAt = ref("");

const isScheduled = computed(() => status.value === "scheduled");

const scheduledForLabel = computed(() =>
  article.value?.scheduledFor ? new Date(article.value.scheduledFor).toLocaleString() : "",
);

async function schedule() {
  formError.value = null;

  if (!scheduleAt.value) {
    formError.value = t("studio.articles.pickDate");
    return;
  }

  const when = new Date(scheduleAt.value);
  if (Number.isNaN(when.getTime()) || when <= new Date()) {
    // The API rejects a past time too; catching it here means the editor is told before a round trip.
    formError.value = t("studio.articles.futureOnly");
    return;
  }

  scheduling.value = true;

  try {
    // Same reason as publish: scheduling acts on the *saved* draft, so an unsaved edit would not be
    // what goes live next Tuesday.
    await save();
    if (formError.value) return;

    const id = article.value?.id;
    if (!id) return;

    article.value = await withAuth((api) => api.scheduleArticle(id, when.toISOString()));
    scheduleAt.value = "";
  } catch (error) {
    formError.value = describe(error);
  } finally {
    scheduling.value = false;
  }
}

async function cancelSchedule() {
  formError.value = null;
  scheduling.value = true;

  try {
    const id = article.value?.id;
    if (!id) return;

    article.value = await withAuth((api) => api.unscheduleArticle(id));
  } catch (error) {
    formError.value = describe(error);
  } finally {
    scheduling.value = false;
  }
}

// ---- Version history (CT-8) ----

const versions = ref<ArticleVersionSummary[]>([]);
const versionsOpen = ref(false);
const versionsLoading = ref(false);
const restoring = ref<number | null>(null);

async function loadVersions() {
  const id = article.value?.id;
  if (!id) return;

  versionsLoading.value = true;

  try {
    versions.value = await withAuth((api) => api.listArticleVersions(id));
  } catch (error) {
    formError.value = describe(error);
  } finally {
    versionsLoading.value = false;
  }
}

async function toggleVersions() {
  versionsOpen.value = !versionsOpen.value;
  if (versionsOpen.value && versions.value.length === 0) await loadVersions();
}

async function restore(version: number) {
  const id = article.value?.id;
  if (!id) return;

  // Restoring overwrites the editor's current draft, which is the one thing here that can lose
  // unsaved work — so it asks first. Publishing does not, because it saves rather than discards.
  if (!confirm(t("studio.articles.restoreConfirm", { version })))
    return;

  formError.value = null;
  restoring.value = version;

  try {
    const restored = await withAuth((api) => api.restoreArticleVersion(id, version));

    // Pull the restored content back into the form; it is the draft now.
    article.value = restored;
    title.value = restored.title;
    summary.value = restored.summary;
    content.value = restored.content;
  } catch (error) {
    formError.value = describe(error);
  } finally {
    restoring.value = null;
  }
}

// The list is stale the moment something is published, so it is refetched rather than patched.
watch(() => article.value?.currentVersion, () => {
  if (versionsOpen.value) void loadVersions();
});

function toggleTag(id: string) {
  tagIds.value = tagIds.value.includes(id)
    ? tagIds.value.filter((t) => t !== id)
    : [...tagIds.value, id];
}

useHead(() => ({ title: isNew.value ? t("studio.articles.new") : title.value || t("studio.articles.editTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <NuxtLink to="/studio" class="text-sm font-medium text-accent-strong hover:underline">← {{ t("studio.articles.back") }}</NuxtLink>
        <h1 class="mt-2 font-display text-2xl font-bold tracking-tight text-ink">
          {{ isNew ? t("studio.articles.new") : title || t("studio.common.untitled") }}
        </h1>
        <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
          <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
          <span v-if="isScheduled">Publishes {{ scheduledForLabel }}</span>
          <span v-if="savedAt">{{ t("studio.common.savedAt", { time: savedAt }) }}</span>
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <DbButton variant="outline" :disabled="saving || publishing" @click="save">
          {{ saving ? t("studio.articles.saving") : t("studio.common.saveDraft") }}
        </DbButton>
        <DbButton
          :variant="isPublished ? 'outline' : 'primary'"
          :disabled="saving || publishing || (isNew && !title)"
          @click="togglePublish"
        >
          {{ publishing ? "Working…" : isPublished ? "Unpublish" : "Publish" }}
        </DbButton>
      </div>
    </div>

    <p
      v-if="formError"
      role="alert"
      class="mt-4 rounded-card border border-danger/30 bg-danger-subtle px-4 py-3 text-sm text-danger"
    >
      {{ formError }}
    </p>

    <div class="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_26rem]">
      <div class="space-y-6">
        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.articles.details") }}</h2>

          <DbInput v-model="title" :label="t('studio.common.title')" required />
          <DbInput v-model="summary" :label="t('studio.common.summary')" :hint="t('studio.articles.summaryHint')" />
          <DbInput
            v-model="slug"
            :label="t('studio.common.slug')"
            :disabled="slugLocked"
            :hint="slugLocked ? t('studio.articles.slugHintLocked') : t('studio.articles.slugHintFree')"
          />

          <div>
            <label for="category" class="mb-1.5 block text-sm font-medium text-ink-muted">{{ t("studio.articles.category") }}</label>
            <select
              id="category"
              v-model="categoryId"
              class="h-10 w-full rounded-control border border-line-strong bg-surface px-3 text-sm"
            >
              <option value="">{{ t("studio.articles.noCategory") }}</option>
              <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </div>

          <fieldset>
            <legend class="mb-1.5 text-sm font-medium text-ink-muted">{{ t("studio.articles.tags") }}</legend>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="tag in allTags"
                :key="tag.id"
                type="button"
                :aria-pressed="tagIds.includes(tag.id)"
                class="rounded-control border px-2 py-0.5 text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
                :class="
                  tagIds.includes(tag.id)
                    ? 'border-accent-strong bg-accent-subtle text-accent-strong'
                    : 'border-line text-ink-muted hover:bg-surface-sunken'
                "
                @click="toggleTag(tag.id)"
              >
                #{{ tag.name }}
              </button>
              <p v-if="!allTags.length" class="text-sm text-ink-subtle">{{ t("studio.articles.noTags") }}</p>
            </div>
          </fieldset>
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.articles.content") }}</h2>
          <BlockEditor v-model="content" />
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.articles.seo") }}</h2>
          <DbInput v-model="metaTitle" :label="t('studio.articles.metaTitle')" :hint="t('studio.articles.metaTitleHint')" />
          <DbInput v-model="metaDescription" :label="t('studio.articles.metaDescription')" :hint="t('studio.articles.metaDescriptionHint')" />
        </section>

        <!-- Scheduling (CT-7). Hidden while the article is published: a published article has to be
             unpublished before it can be scheduled, and offering a control that always errors is
             worse than not offering it. -->
        <section
          v-if="!isNew && !isPublished"
          class="space-y-4 rounded-card border border-line bg-surface p-5"
        >
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
            {{ t("studio.articles.schedule") }}
          </h2>

          <template v-if="isScheduled">
            <p class="text-sm text-ink-muted">
              {{ t("studio.articles.publishesOn") }}
              <strong class="text-ink">{{ scheduledForLabel }}</strong
              >. Editing the draft before then is fine — whatever is saved at that moment goes live.
            </p>
            <DbButton variant="outline" size="sm" :disabled="scheduling" @click="cancelSchedule">
              {{ scheduling ? t("studio.common.working") : t("studio.articles.cancelSchedule") }}
            </DbButton>
          </template>

          <template v-else>
            <div>
              <label for="schedule-at" class="mb-1.5 block text-sm font-medium text-ink-muted">
                {{ t("studio.articles.publishAt") }}
              </label>
              <input
                id="schedule-at"
                v-model="scheduleAt"
                type="datetime-local"
                class="h-10 w-full rounded-control border border-line-strong bg-surface px-3 text-sm"
              />
              <p class="mt-1.5 text-xs text-ink-subtle">
                {{ t("studio.articles.publishAtHint") }}
              </p>
            </div>
            <DbButton variant="outline" size="sm" :disabled="scheduling || saving" @click="schedule">
              {{ scheduling ? t("studio.articles.scheduling") : t("studio.articles.schedule") }}
            </DbButton>
          </template>
        </section>

        <!-- Version history (CT-8). Collapsed by default and loaded on demand: most edits never
             need it, and it is one request per open rather than one per page load. -->
        <section v-if="!isNew" class="space-y-4 rounded-card border border-line bg-surface p-5">
          <div class="flex items-center justify-between">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
              {{ t("studio.articles.versionHistory") }}
            </h2>
            <button
              type="button"
              class="text-sm font-medium text-accent-strong hover:underline"
              :aria-expanded="versionsOpen"
              @click="toggleVersions"
            >
              {{ versionsOpen ? t("studio.common.hide") : t("studio.common.show") }}
            </button>
          </div>

          <template v-if="versionsOpen">
            <p v-if="versionsLoading" class="text-sm text-ink-muted">{{ t("studio.common.loading") }}</p>
            <p v-else-if="versions.length === 0" class="text-sm text-ink-muted">
              {{ t("studio.articles.noVersions") }}
            </p>

            <ul v-else class="divide-y divide-line">
              <li v-for="v in versions" :key="v.version" class="flex items-start gap-3 py-3">
                <div class="min-w-0 flex-1">
                  <p class="flex items-center gap-2 text-sm font-medium text-ink">
                    v{{ v.version }}
                    <DbChip v-if="v.isCurrent" tone="success">{{ t("studio.articles.live") }}</DbChip>
                  </p>
                  <p class="truncate text-sm text-ink-muted">{{ v.title }}</p>
                  <p class="text-xs text-ink-subtle">
                    {{ new Date(v.createdAt).toLocaleString() }} · {{ v.readingTimeMinutes }} min
                  </p>
                </div>
                <DbButton
                  variant="ghost"
                  size="sm"
                  :disabled="restoring !== null"
                  @click="restore(v.version)"
                >
                  {{ restoring === v.version ? t("studio.articles.restoring") : t("studio.articles.restore") }}
                </DbButton>
              </li>
            </ul>

            <p class="text-xs text-ink-subtle">
              Restoring copies a version into the draft. History is never rewritten, and nothing
              published changes until you publish again.
            </p>
          </template>
        </section>
      </div>

      <!-- Live preview through the *same* ContentRenderer the public site uses. That shared
           registry is the reason preview and production cannot drift (ADR-0007). -->
      <aside class="xl:sticky xl:top-6 xl:self-start">
        <div class="rounded-card border border-line bg-surface">
          <div class="border-b border-line px-4 py-2">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.articles.preview") }}</h2>
          </div>
          <div class="max-h-[70vh] overflow-y-auto p-5">
            <h3 class="font-display text-xl font-bold tracking-tight text-ink">
              {{ title || t("studio.common.untitled") }}
            </h3>
            <p v-if="summary" class="mt-2 text-sm text-ink-muted">{{ summary }}</p>
            <hr class="my-4 border-line" />
            <!-- showUnknownBlocks: an author must see that a block exists even when this build
                 cannot render it; a reader must not (CONTENT_MODEL invariants). -->
            <ContentRenderer
              :document="content"
              :resolve-media="resolveMedia"
              show-unknown-blocks
            />
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>
