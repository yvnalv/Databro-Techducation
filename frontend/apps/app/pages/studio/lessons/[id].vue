<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { ContentRenderer, DbButton, DbChip, DbInput, mediaResolverFor } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";
import type { ArticleVersionSummary, ContentDocument, LessonContent } from "@databro/types";

const { t } = useI18n();

/**
 * Lesson body editor.
 *
 * Deliberately the article editor minus everything a lesson does not have: no taxonomy, no SEO, no
 * scheduling. It reuses the same `BlockEditor` and the same preview renderer, which is ADR-0007
 * paying out at the UI layer too — one editor, not two that drift.
 *
 * Where the lesson *sits* is not edited here. Position, objectives and difficulty belong to the
 * curriculum, so they live in the course builder.
 */
const route = useRoute();
const router = useRouter();
const { withAuth } = useAuth();

const lessonId = computed(() => String(route.params.id));
const isNew = computed(() => lessonId.value === "new");

const { data: loaded } = await useAsyncData(
  () => `authoring:lesson:${lessonId.value}`,
  async () => (isNew.value ? null : await withAuth((api) => api.getLessonContent(lessonId.value))),
  { watch: [lessonId] },
);

const lesson = ref<LessonContent | null>(loaded.value ?? null);

const title = ref(lesson.value?.title ?? "");
const summary = ref(lesson.value?.summary ?? "");
const slug = ref(lesson.value?.slug ?? "");
const content = ref<ContentDocument>(lesson.value?.content ?? { version: 1, blocks: [] });

const busy = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);

const status = computed(() => lesson.value?.status ?? "draft");
const isPublished = computed(() => status.value === "published");
const slugLocked = computed(() => Boolean(lesson.value?.publishedAt));

const { merged } = useMediaCache();
const resolveMedia = computed(() => mediaResolverFor(merged(undefined)));

function describe(error: unknown) {
  if (error instanceof ApiClientError) return error.message;
  return t("studio.common.genericError");
}

async function save() {
  formError.value = null;
  busy.value = true;

  try {
    if (isNew.value) {
      const created = await withAuth((api) =>
        api.createLessonContent({
          title: title.value,
          summary: summary.value,
          content: content.value,
          slug: slug.value || undefined,
        }),
      );

      lesson.value = created;
      // Move off /new so a reload cannot create a second body.
      await router.replace(`/studio/lessons/${created.id}`);
      return;
    }

    lesson.value = await withAuth((api) =>
      api.updateLessonContent(lessonId.value, {
        title: title.value,
        summary: summary.value,
        content: content.value,
      }),
    );

    savedAt.value = new Date().toLocaleTimeString();
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

async function togglePublish() {
  formError.value = null;
  busy.value = true;

  try {
    // Publish snapshots the *saved* draft, so an unsaved edit would silently not go live.
    await save();
    if (formError.value) return;

    const id = lesson.value?.id;
    if (!id) return;

    lesson.value = isPublished.value
      ? await withAuth((api) => api.unpublishLessonContent(id))
      : await withAuth((api) => api.publishLessonContent(id));
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

// ---- Version history, free from the shared engine ----

const versions = ref<ArticleVersionSummary[]>([]);
const versionsOpen = ref(false);
const restoring = ref<number | null>(null);

async function toggleVersions() {
  versionsOpen.value = !versionsOpen.value;
  if (!versionsOpen.value || versions.value.length || !lesson.value) return;

  try {
    versions.value = await withAuth((api) => api.listLessonContentVersions(lesson.value!.id));
  } catch (error) {
    formError.value = describe(error);
  }
}

async function restore(version: number) {
  const id = lesson.value?.id;
  if (!id) return;

  if (!confirm(t("studio.lessons.restoreConfirm", { version })))
    return;

  restoring.value = version;

  try {
    const restored = await withAuth((api) => api.restoreLessonContentVersion(id, version));
    lesson.value = restored;
    title.value = restored.title;
    summary.value = restored.summary;
    content.value = restored.content;
  } catch (error) {
    formError.value = describe(error);
  } finally {
    restoring.value = null;
  }
}

watch(() => lesson.value?.currentVersion, () => {
  if (versionsOpen.value) versions.value = [];
});

useHead(() => ({ title: isNew.value ? t("studio.lessons.newTitle") : title.value || t("studio.lessons.editTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <NuxtLink to="/studio/lessons" class="text-sm font-medium text-accent hover:underline">← {{ t("studio.lessons.back") }}</NuxtLink>
        <h1 class="mt-2 font-display text-2xl font-bold tracking-tight text-ink">
          {{ isNew ? t("studio.lessons.newTitle") : title || t("studio.common.untitled") }}
        </h1>
        <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
          <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
          <span v-if="savedAt">{{ t("studio.common.savedAt", { time: savedAt }) }}</span>
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <DbButton variant="outline" :disabled="busy" @click="save">
          {{ busy ? t("studio.common.working") : isNew ? t("studio.lessons.create") : t("studio.common.saveDraft") }}
        </DbButton>
        <DbButton
          v-if="!isNew"
          :variant="isPublished ? 'outline' : 'primary'"
          :disabled="busy"
          @click="togglePublish"
        >
          {{ isPublished ? t("studio.common.unpublish") : t("studio.common.publish") }}
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

    <p
      v-if="isPublished"
      class="mt-4 rounded-card border border-line bg-surface-sunken px-4 py-3 text-sm text-ink-muted"
    >
      {{ t("studio.lessons.unpublishWarning") }}
    </p>

    <div class="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_26rem]">
      <div class="space-y-6">
        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.lessons.details") }}</h2>

          <DbInput v-model="title" :label="t('studio.common.title')" required />
          <DbInput v-model="summary" :label="t('studio.common.summary')" :hint="t('studio.lessons.summaryHint')" />
          <DbInput
            v-if="isNew"
            v-model="slug"
            :label="t('studio.common.slug')"
            :hint="t('studio.lessons.slugHint')"
          />
          <p v-else class="text-sm text-ink-muted">
            {{ t("studio.lessons.slugLabel") }} <code class="font-mono">{{ lesson?.slug }}</code>
            <span v-if="slugLocked" class="text-ink-subtle">{{ t("studio.lessons.slugLocked") }}</span>
          </p>
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.lessons.content") }}</h2>
          <BlockEditor v-model="content" />
        </section>

        <section v-if="!isNew" class="space-y-4 rounded-card border border-line bg-surface p-5">
          <div class="flex items-center justify-between">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
              {{ t("studio.lessons.versionHistory") }}
            </h2>
            <button
              type="button"
              class="text-sm font-medium text-accent hover:underline"
              :aria-expanded="versionsOpen"
              @click="toggleVersions"
            >
              {{ versionsOpen ? t("studio.common.hide") : t("studio.common.show") }}
            </button>
          </div>

          <template v-if="versionsOpen">
            <p v-if="versions.length === 0" class="text-sm text-ink-muted">
              {{ t("studio.lessons.noVersions") }}
            </p>
            <ul v-else class="divide-y divide-line">
              <li v-for="v in versions" :key="v.version" class="flex items-start gap-3 py-3">
                <div class="min-w-0 flex-1">
                  <p class="flex items-center gap-2 text-sm font-medium text-ink">
                    v{{ v.version }}
                    <DbChip v-if="v.isCurrent" tone="success">{{ t("studio.lessons.live") }}</DbChip>
                  </p>
                  <p class="truncate text-sm text-ink-muted">{{ v.title }}</p>
                  <p class="text-xs text-ink-subtle">{{ new Date(v.createdAt).toLocaleString() }}</p>
                </div>
                <DbButton
                  variant="ghost"
                  size="sm"
                  :disabled="restoring !== null"
                  @click="restore(v.version)"
                >
                  {{ restoring === v.version ? t("studio.lessons.restoring") : t("studio.lessons.restore") }}
                </DbButton>
              </li>
            </ul>
          </template>
        </section>
      </div>

      <aside class="xl:sticky xl:top-6 xl:self-start">
        <div class="rounded-card border border-line bg-surface">
          <div class="border-b border-line px-4 py-2">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.lessons.preview") }}</h2>
          </div>
          <div class="max-h-[70vh] overflow-y-auto p-5">
            <h3 class="font-display text-xl font-bold tracking-tight text-ink">{{ title || t("studio.common.untitled") }}</h3>
            <p v-if="summary" class="mt-2 text-sm text-ink-muted">{{ summary }}</p>
            <hr class="my-4 border-line" />
            <!-- The same renderer the public site uses, so preview and production cannot drift. -->
            <ContentRenderer :document="content" :resolve-media="resolveMedia" show-unknown-blocks />
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>
