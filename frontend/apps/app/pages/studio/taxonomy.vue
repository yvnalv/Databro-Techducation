<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip, DbInput } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";
import type { Category, TaxonomyTerm } from "@databro/types";

const { t } = useI18n();

/**
 * Taxonomy management (docs/BUSINESS_RULES.md TX-1 … TX-3).
 *
 * Categories and tags together on one screen: they are the same job — curating the vocabulary
 * articles are filed under — and splitting them would mean two pages that are each half-empty.
 */
const { withAuth } = useAuth();

const { data, refresh, error: loadError } = await useAsyncData(
  "taxonomy",
  async () => {
    const [categories, tags] = await Promise.all([
      withAuth((api) => api.listCategories()),
      withAuth((api) => api.listTags()),
    ]);
    return { categories, tags };
  },
  { default: () => ({ categories: [] as Category[], tags: [] as TaxonomyTerm[] }) },
);

const categories = computed(() => data.value?.categories ?? []);
const tags = computed(() => data.value?.tags ?? []);

const message = ref<{ tone: "danger" | "success"; text: string } | null>(null);
const busy = ref(false);

function describe(error: unknown) {
  return error instanceof ApiClientError ? error.message : t("studio.taxonomy.genericError");
}

/** Every mutation goes through here so success and failure are reported one way. */
async function run(action: () => Promise<unknown>, success: string) {
  busy.value = true;
  message.value = null;
  try {
    await action();
    await refresh();
    message.value = { tone: "success", text: success };
  } catch (error) {
    message.value = { tone: "danger", text: describe(error) };
  } finally {
    busy.value = false;
  }
}

// ---- Categories ----

const newCategory = reactive({ name: "", slug: "", parentId: "" });

const addCategory = () =>
  run(
    () =>
      withAuth((api) =>
        api.createCategory({
          name: newCategory.name,
          slug: newCategory.slug || undefined,
          parentId: newCategory.parentId || null,
        }),
      ),
    t("studio.taxonomy.categoryCreated"),
  ).then(() => {
    if (message.value?.tone === "success") Object.assign(newCategory, { name: "", slug: "", parentId: "" });
  });

const editingCategory = ref<string | null>(null);
const categoryDraft = reactive({ name: "", description: "", order: "0", parentId: "" });

function beginEditCategory(category: Category) {
  editingCategory.value = category.id;
  Object.assign(categoryDraft, {
    name: category.name,
    description: category.description ?? "",
    order: String(category.order),
    parentId: category.parentId ?? "",
  });
}

const saveCategory = (id: string) =>
  run(
    () =>
      withAuth((api) =>
        api.updateCategory(id, {
          name: categoryDraft.name,
          description: categoryDraft.description || undefined,
          order: Number(categoryDraft.order) || 0,
          parentId: categoryDraft.parentId || null,
        }),
      ),
    t("studio.taxonomy.categoryUpdated"),
  ).then(() => {
    if (message.value?.tone === "success") editingCategory.value = null;
  });

// TX-2: the API refuses while articles or children still reference it, and says how many.
const removeCategory = (category: Category) =>
  run(() => withAuth((api) => api.deleteCategory(category.id)), `Deleted “${category.name}”.`);

/** Parent options exclude the category itself — the domain rejects that cycle anyway (TX-3). */
const parentOptions = (excludeId?: string) =>
  categories.value.filter((c) => c.id !== excludeId);

const categoryName = (id?: string | null) =>
  categories.value.find((c) => c.id === id)?.name ?? "—";

// ---- Tags ----

const newTag = reactive({ name: "", slug: "" });

const addTag = () =>
  run(
    () => withAuth((api) => api.createTag({ name: newTag.name, slug: newTag.slug || undefined })),
    t("studio.taxonomy.tagCreated"),
  ).then(() => {
    if (message.value?.tone === "success") Object.assign(newTag, { name: "", slug: "" });
  });

const editingTag = ref<string | null>(null);
const tagDraft = ref("");

function beginEditTag(tag: TaxonomyTerm) {
  editingTag.value = tag.id;
  tagDraft.value = tag.name;
}

const saveTag = (id: string) =>
  run(() => withAuth((api) => api.updateTag(id, { name: tagDraft.value })), t("studio.taxonomy.tagRenamed")).then(() => {
    if (message.value?.tone === "success") editingTag.value = null;
  });

const removeTag = (tag: TaxonomyTerm) =>
  run(() => withAuth((api) => api.deleteTag(tag.id)), `Deleted “${tag.name}”.`);

useHead(() => ({ title: t("studio.taxonomy.navTitle") }));
</script>

<template>
  <div>
    <h1 class="font-display text-2xl font-bold tracking-tight text-ink">{{ t("studio.taxonomy.navTitle") }}</h1>
    <p class="mt-1 text-sm text-ink-muted">
      {{ t("studio.taxonomy.subtitle") }}
    </p>

    <p
      v-if="message"
      :role="message.tone === 'danger' ? 'alert' : 'status'"
      class="mt-4 rounded-card border px-4 py-3 text-sm"
      :class="
        message.tone === 'danger'
          ? 'border-danger/30 bg-danger-subtle text-danger'
          : 'border-success/30 bg-success-subtle text-success'
      "
    >
      {{ message.text }}
    </p>

    <p v-if="loadError" role="alert" class="mt-4 text-sm text-danger">{{ t("studio.taxonomy.loadFailed") }}</p>

    <div class="mt-6 grid gap-6 xl:grid-cols-2">
      <!-- Categories -->
      <section class="rounded-card border border-line bg-surface-raised">
        <header class="border-b border-line px-5 py-3">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
            {{ t("studio.taxonomy.categories") }}
          </h2>
        </header>

        <ul class="divide-y divide-line">
          <li v-for="category in categories" :key="category.id" class="px-5 py-3">
            <div v-if="editingCategory === category.id" class="space-y-3">
              <DbInput v-model="categoryDraft.name" :label="t('studio.common.name')" />
              <DbInput v-model="categoryDraft.description" :label="t('studio.common.description')" />

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label :for="`parent-${category.id}`" class="mb-1.5 block text-sm font-medium text-ink-muted">
                    {{ t("studio.taxonomy.parent") }}
                  </label>
                  <select
                    :id="`parent-${category.id}`"
                    v-model="categoryDraft.parentId"
                    class="h-10 w-full rounded-control border border-line-strong bg-surface-raised px-3 text-sm"
                  >
                    <option value="">{{ t("studio.taxonomy.topLevel") }}</option>
                    <option v-for="c in parentOptions(category.id)" :key="c.id" :value="c.id">
                      {{ c.name }}
                    </option>
                  </select>
                </div>
                <DbInput v-model="categoryDraft.order" :label="t('studio.common.order')" type="number" />
              </div>

              <div class="flex gap-2">
                <DbButton size="sm" :disabled="busy" @click="saveCategory(category.id)">{{ t("studio.common.save") }}</DbButton>
                <DbButton size="sm" variant="ghost" @click="editingCategory = null">{{ t("studio.common.cancel") }}</DbButton>
              </div>
            </div>

            <div v-else class="flex flex-wrap items-center justify-between gap-3">
              <div class="min-w-0">
                <p class="flex flex-wrap items-center gap-2">
                  <span class="font-medium text-ink">{{ category.name }}</span>
                  <DbChip tone="category">{{ category.articleCount }} published</DbChip>
                </p>
                <p class="mt-0.5 font-mono text-xs text-ink-subtle">
                  /{{ category.slug }}
                  <span v-if="category.parentId"> · {{ t("studio.taxonomy.under", { parent: categoryName(category.parentId) }) }}</span>
                </p>
              </div>

              <span class="flex gap-2">
                <DbButton size="sm" variant="outline" @click="beginEditCategory(category)">{{ t("studio.common.edit") }}</DbButton>
                <DbButton size="sm" variant="ghost" :disabled="busy" @click="removeCategory(category)">
                  {{ t("studio.common.delete") }}
                </DbButton>
              </span>
            </div>
          </li>

          <li v-if="!categories.length" class="px-5 py-6 text-sm text-ink-muted">{{ t("studio.taxonomy.noCategories") }}</li>
        </ul>

        <div class="space-y-3 border-t border-line bg-surface-sunken p-5">
          <h3 class="text-sm font-medium text-ink">{{ t("studio.taxonomy.addCategory") }}</h3>
          <DbInput v-model="newCategory.name" :label="t('studio.common.name')" />
          <DbInput
            v-model="newCategory.slug"
            :label="t('studio.common.slug')"
            :hint="t('studio.taxonomy.categorySlugHint')"
          />
          <div>
            <label for="new-parent" class="mb-1.5 block text-sm font-medium text-ink-muted">{{ t("studio.taxonomy.parent") }}</label>
            <select
              id="new-parent"
              v-model="newCategory.parentId"
              class="h-10 w-full rounded-control border border-line-strong bg-surface-raised px-3 text-sm"
            >
              <option value="">{{ t("studio.taxonomy.topLevel") }}</option>
              <option v-for="c in categories" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </div>
          <DbButton :disabled="busy || !newCategory.name" @click="addCategory">{{ t("studio.taxonomy.createCategory") }}</DbButton>
        </div>
      </section>

      <!-- Tags -->
      <section class="rounded-card border border-line bg-surface-raised">
        <header class="border-b border-line px-5 py-3">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">{{ t("studio.taxonomy.tags") }}</h2>
        </header>

        <ul class="divide-y divide-line">
          <li v-for="tag in tags" :key="tag.id" class="px-5 py-3">
            <div v-if="editingTag === tag.id" class="space-y-3">
              <DbInput v-model="tagDraft" :label="t('studio.common.name')" />
              <div class="flex gap-2">
                <DbButton size="sm" :disabled="busy" @click="saveTag(tag.id)">{{ t("studio.common.save") }}</DbButton>
                <DbButton size="sm" variant="ghost" @click="editingTag = null">{{ t("studio.common.cancel") }}</DbButton>
              </div>
            </div>

            <div v-else class="flex flex-wrap items-center justify-between gap-3">
              <div class="min-w-0">
                <p class="font-medium text-ink">#{{ tag.name }}</p>
                <p class="mt-0.5 font-mono text-xs text-ink-subtle">/{{ tag.slug }}</p>
              </div>
              <span class="flex gap-2">
                <DbButton size="sm" variant="outline" @click="beginEditTag(tag)">{{ t("studio.common.edit") }}</DbButton>
                <DbButton size="sm" variant="ghost" :disabled="busy" @click="removeTag(tag)">{{ t("studio.common.delete") }}</DbButton>
              </span>
            </div>
          </li>

          <li v-if="!tags.length" class="px-5 py-6 text-sm text-ink-muted">{{ t("studio.taxonomy.noTags") }}</li>
        </ul>

        <div class="space-y-3 border-t border-line bg-surface-sunken p-5">
          <h3 class="text-sm font-medium text-ink">{{ t("studio.taxonomy.addTag") }}</h3>
          <DbInput v-model="newTag.name" :label="t('studio.common.name')" />
          <DbInput v-model="newTag.slug" :label="t('studio.common.slug')" :hint="t('studio.taxonomy.tagSlugHint')" />
          <DbButton :disabled="busy || !newTag.name" @click="addTag">{{ t("studio.taxonomy.createTag") }}</DbButton>
        </div>
      </section>
    </div>
  </div>
</template>
