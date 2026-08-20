<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip } from "@databro/ui";
import type { ArticleSummary, Paged } from "@databro/types";

const { t } = useI18n();

// Resolved once: DbButton takes the component so `@databro/ui` need not import NuxtLink.
const NuxtLink = resolveComponent("NuxtLink");

/**
 * Article list — the CMS's home surface (docs/UI_PATTERNS.md §7).
 *
 * Reads the *authoring* endpoint, which returns every status. The public listing is a different
 * thing entirely: published only, cached, and indexable.
 */
const route = useRoute();
const { withAuth } = useAuth();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data, error, refresh } = await useAsyncData<Paged<ArticleSummary>>(
  () => `authoring:articles:${page.value}`,
  () => withAuth((api) => api.listAuthoringArticles({ page: page.value, pageSize: 20 })),
  { watch: [page] },
);

const articles = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

/**
 * Counts are for *this page*, and say so in the label. Deriving a total from a page and calling it
 * a total would be wrong the moment there is more than one page — the API has no status-count
 * endpoint yet, and inventing one in the UI would just be a lie with a number on it.
 */
const counts = computed(() => ({
  total: meta.value?.total ?? 0,
  published: articles.value.filter((a) => a.status === "published").length,
  drafts: articles.value.filter((a) => a.status === "draft").length,
  scheduled: articles.value.filter((a) => a.status === "scheduled").length,
}));

const STATUS_TONE = {
  published: "success",
  draft: "neutral",
  scheduled: "info",
  unpublished: "warning",
  archived: "neutral",
} as const;

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString("en", { year: "numeric", month: "short", day: "numeric" }) : "—";

useHead(() => ({ title: t("studio.articles.navTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="font-display text-2xl font-bold tracking-tight text-ink">{{ t("studio.articles.navTitle") }}</h1>
        <p class="mt-1 text-sm text-ink-muted">{{ t("studio.articles.subtitle") }}</p>
      </div>

      <DbButton :as="NuxtLink" to="/studio/articles/new">{{ t("studio.articles.new") }}</DbButton>
    </div>

    <!-- Stat cards, per the reference dashboard. Scoped to the current page (see `counts`). -->
    <dl class="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <div
        v-for="stat in [
          { label: t('studio.articles.statTotal'), value: counts.total, hint: t('studio.articles.hintAllPages') },
          { label: t('studio.articles.statPublished'), value: counts.published, hint: t('studio.articles.hintThisPage') },
          { label: t('studio.articles.statDrafts'), value: counts.drafts, hint: t('studio.articles.hintThisPage') },
          { label: t('studio.articles.statScheduled'), value: counts.scheduled, hint: t('studio.articles.hintThisPage') },
        ]"
        :key="stat.label"
        class="rounded-card border border-line bg-surface p-5"
      >
        <dt class="text-sm text-ink-muted">{{ stat.label }}</dt>
        <dd class="mt-1 font-display text-2xl font-bold text-ink">
          {{ stat.value }}
          <span class="ms-1 align-middle text-xs font-normal text-ink-subtle">{{ stat.hint }}</span>
        </dd>
      </div>
    </dl>

    <p
      v-if="error"
      role="alert"
      class="mt-6 rounded-card border border-danger/30 bg-danger-subtle px-4 py-3 text-sm text-danger"
    >
      {{ t("studio.articles.loadFailed") }}
      <button type="button" class="font-semibold underline" @click="refresh()">{{ t("studio.common.retry") }}</button>
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line bg-surface">
      <div class="overflow-x-auto">
        <table class="w-full text-left text-sm">
          <!-- Dark header row: the reference reserves this for dashboards, and this is the one
               sanctioned use (DESIGN_SYSTEM §5.7). -->
          <thead class="bg-accent-deep text-ink-on-deep">
            <tr>
              <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.title") }}</th>
              <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.status") }}</th>
              <th scope="col" class="hidden px-4 py-3 font-semibold md:table-cell">{{ t("studio.articles.colCategory") }}</th>
              <th scope="col" class="hidden px-4 py-3 font-semibold lg:table-cell">{{ t("studio.articles.colAuthor") }}</th>
              <th scope="col" class="hidden px-4 py-3 font-semibold lg:table-cell">{{ t("studio.common.published") }}</th>
            </tr>
          </thead>

          <tbody>
            <tr v-if="!articles.length">
              <td colspan="5" class="px-4 py-10 text-center text-ink-muted">
                {{ t("studio.articles.empty") }}
              </td>
            </tr>

            <tr
              v-for="article in articles"
              :key="article.id"
              class="border-t border-line align-middle"
            >
              <td class="px-4 py-3">
                <NuxtLink
                  :to="`/studio/articles/${article.id}`"
                  class="font-medium text-ink transition-colors hover:text-accent-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
                >
                  {{ article.title }}
                </NuxtLink>
                <span class="mt-0.5 block font-mono text-xs text-ink-subtle">/{{ article.slug }}</span>
              </td>
              <td class="px-4 py-3">
                <!-- Chip carries the status word as well as the tint: colour never alone. -->
                <DbChip :tone="STATUS_TONE[article.status] ?? 'neutral'">
                  {{ article.status }}
                </DbChip>
              </td>
              <td class="hidden px-4 py-3 text-ink-muted md:table-cell">
                {{ article.category?.name ?? "—" }}
              </td>
              <td class="hidden px-4 py-3 text-ink-muted lg:table-cell">
                {{ article.author?.displayName ?? "—" }}
              </td>
              <td class="hidden px-4 py-3 text-ink-muted lg:table-cell">
                {{ formatDate(article.publishedAt) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div
        v-if="meta && meta.totalPages > 1"
        class="flex items-center justify-between gap-4 border-t border-line px-4 py-3 text-sm text-ink-muted"
      >
        <span>Showing page {{ meta.page }} of {{ meta.totalPages }} — {{ meta.total }} articles</span>

        <span class="flex gap-1">
          <NuxtLink
            v-if="meta.page > 1"
            :to="{ query: { page: meta.page - 1 } }"
            class="rounded-control px-3 py-1.5 font-medium hover:bg-surface-sunken hover:text-ink"
          >
            {{ t("studio.common.previous") }}
          </NuxtLink>
          <NuxtLink
            v-if="meta.page < meta.totalPages"
            :to="{ query: { page: meta.page + 1 } }"
            class="rounded-control px-3 py-1.5 font-medium hover:bg-surface-sunken hover:text-ink"
          >
            {{ t("studio.common.next") }}
          </NuxtLink>
        </span>
      </div>
    </div>
  </div>
</template>
