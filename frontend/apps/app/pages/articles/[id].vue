<script setup lang="ts">
import { ContentRenderer, DbButton, DbChip, DbInput, mediaResolverFor } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";
import type { Article, Category, ContentDocument, TaxonomyTerm } from "@databro/types";

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

if (loadError.value) throw createError({ statusCode: 404, statusMessage: "Article not found", fatal: true });

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
  return "Something went wrong. Please try again.";
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
    if (isNew.value) await router.replace(`/articles/${saved.id}`);
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

function toggleTag(id: string) {
  tagIds.value = tagIds.value.includes(id)
    ? tagIds.value.filter((t) => t !== id)
    : [...tagIds.value, id];
}

useHead({ title: computed(() => (isNew.value ? "New article" : title.value || "Edit article")) });
</script>

<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <NuxtLink to="/" class="text-sm font-medium text-accent hover:underline">← Articles</NuxtLink>
        <h1 class="mt-2 font-display text-2xl font-bold tracking-tight text-ink">
          {{ isNew ? "New article" : title || "Untitled" }}
        </h1>
        <p class="mt-1 flex items-center gap-2 text-sm text-ink-muted">
          <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
          <span v-if="savedAt">Saved {{ savedAt }}</span>
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <DbButton variant="outline" :disabled="saving || publishing" @click="save">
          {{ saving ? "Saving…" : "Save draft" }}
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
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Details</h2>

          <DbInput v-model="title" label="Title" required />
          <DbInput v-model="summary" label="Summary" hint="Used as the excerpt and meta description fallback." />
          <DbInput
            v-model="slug"
            label="Slug"
            :disabled="slugLocked"
            :hint="slugLocked ? 'Immutable once published — moving it needs a 301 redirect.' : 'Leave blank to derive from the title.'"
          />

          <div>
            <label for="category" class="mb-1.5 block text-sm font-medium text-ink-muted">Category</label>
            <select
              id="category"
              v-model="categoryId"
              class="h-10 w-full rounded-md border border-line-strong bg-surface px-3 text-sm"
            >
              <option value="">No category</option>
              <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </div>

          <fieldset>
            <legend class="mb-1.5 text-sm font-medium text-ink-muted">Tags</legend>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="tag in allTags"
                :key="tag.id"
                type="button"
                :aria-pressed="tagIds.includes(tag.id)"
                class="rounded-sm border px-2 py-0.5 text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
                :class="
                  tagIds.includes(tag.id)
                    ? 'border-accent bg-accent-subtle text-accent'
                    : 'border-line text-ink-muted hover:bg-surface-sunken'
                "
                @click="toggleTag(tag.id)"
              >
                #{{ tag.name }}
              </button>
              <p v-if="!allTags.length" class="text-sm text-ink-subtle">No tags defined yet.</p>
            </div>
          </fieldset>
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Content</h2>
          <BlockEditor v-model="content" />
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">SEO</h2>
          <DbInput v-model="metaTitle" label="Meta title" hint="Falls back to the article title." />
          <DbInput v-model="metaDescription" label="Meta description" hint="Falls back to the summary." />
        </section>
      </div>

      <!-- Live preview through the *same* ContentRenderer the public site uses. That shared
           registry is the reason preview and production cannot drift (ADR-0007). -->
      <aside class="xl:sticky xl:top-6 xl:self-start">
        <div class="rounded-card border border-line bg-surface">
          <div class="border-b border-line px-4 py-2">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Preview</h2>
          </div>
          <div class="max-h-[70vh] overflow-y-auto p-5">
            <h3 class="font-display text-xl font-bold tracking-tight text-ink">
              {{ title || "Untitled" }}
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
