<script setup lang="ts">
import type { SearchResults } from "@databro/types";

/**
 * Search results (ADR-0010).
 *
 * **Deliberately `noindex, follow`.** Internal search result pages are the textbook case of thin,
 * near-duplicate, infinitely-generatable content; Google's own guidance is to keep them out of the
 * index. `follow` is kept so the articles listed here still receive the crawl path. robots.txt
 * disallows `/search` as well — belt and braces, since a disallowed page cannot be crawled to
 * discover its own noindex.
 *
 * Rendered server-side rather than as a client-only widget so a shared or bookmarked result URL
 * loads complete, and so the page works before hydration.
 */
const { t, locale } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();

const query = computed(() => String(route.query.q ?? "").trim());

// No page: segments are shown side by side and capped for display rather than paged (ADR-0014).
const empty: SearchResults = { query: "", segments: [] };

const { data, error } = await useAsyncData(
  () => `search:${locale.value}:${query.value}`,
  async (): Promise<SearchResults> => {
    // Skipping the request for an empty query keeps the landing state ("type something") free of a
    // pointless round trip.
    if (!query.value) return empty;

    return client.search({ q: query.value, locale: locale.value });
  },
  { watch: [query, locale] },
);

const results = computed<SearchResults>(() => data.value ?? empty);

/**
 * Segments with something in them, in the order the API chose (courses first).
 *
 * Empty segments are dropped rather than rendered as "Courses (0)": a heading over nothing is noise,
 * and the total across everything already tells a reader whether the search found anything at all.
 */
const populated = computed(() => results.value.segments.filter((s) => s.hits.length > 0));

const totalHits = computed(() => results.value.segments.reduce((sum, s) => sum + s.total, 0));

/**
 * True only when *every* populated segment fell back. A mixed result — an exact course beside
 * corrected articles — is not "approximate results", and saying so would misdescribe half of them.
 */
const allFuzzy = computed(
  () => populated.value.length > 0 && populated.value.every((s) => s.matchMode === "fuzzy"),
);

// No page-range guard here, unlike the indexable listings: search is noindexed and segments are not
// paged, so there is no out-of-range page to 404.

const heading = computed(() =>
  query.value ? t("search.resultsTitle", { query: query.value }) : t("search.pageTitle"),
);

useSeoMeta({
  title: heading.value,
  description: t("search.description"),
  robots: "noindex, follow",
});
</script>

<template>
  <div>
    <PageHeader :eyebrow="t('search.eyebrow')" :title="heading">
      <template #meta>
        <div class="mx-auto mt-6 max-w-xl">
          <SearchBox :initial-query="query" />
        </div>
        <p v-if="query && !error" class="mt-4 text-sm text-ink-on-deep/75">
          {{ t("search.resultCount", totalHits) }}
        </p>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <p v-if="error" class="text-center text-ink-muted">{{ t("search.failed") }}</p>

      <p v-else-if="!query" class="text-center text-ink-muted">{{ t("search.prompt") }}</p>

      <template v-else-if="populated.length">
        <!-- Says out loud that these are approximate. Presenting fallback matches as if they were
             exact is how a search box quietly stops being trusted. -->
        <p
          v-if="allFuzzy"
          class="mb-8 rounded-card border border-line bg-surface-sunken px-4 py-3 text-sm text-ink-muted"
        >
          {{ t("search.fuzzyNotice", { query }) }}
        </p>

        <!-- Segmented, not merged (ADR-0014). A course and an article are different commitments,
             and the scores behind them are not comparable, so the page shows which is which rather
             than inventing one ranking across both. -->
        <div class="space-y-12">
          <section v-for="segment in populated" :key="segment.kind">
            <div class="flex flex-wrap items-baseline gap-3 border-b border-line pb-3">
              <h2 class="font-display text-xl font-bold tracking-tight text-ink">
                {{ t(`search.kind.${segment.kind}`) }}
              </h2>
              <span class="text-sm text-ink-muted">
                {{ t("search.resultCount", segment.total) }}
              </span>
              <span
                v-if="segment.matchMode === 'fuzzy' && !allFuzzy"
                class="text-sm text-ink-subtle"
              >
                {{ t("search.fuzzySegment") }}
              </span>
            </div>

            <ul class="mt-5 space-y-5">
              <li v-for="hit in segment.hits" :key="hit.id">
                <NuxtLink
                  :to="localePath(hit.path)"
                  class="group block rounded-card border border-line bg-surface-raised p-5 transition-shadow hover:shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
                >
                  <h3 class="font-display text-lg font-bold tracking-tight text-ink group-hover:text-accent-strong">
                    {{ hit.title }}
                  </h3>
                  <p v-if="hit.summary" class="mt-1.5 text-sm leading-relaxed text-ink-muted">
                    {{ hit.summary }}
                  </p>
                </NuxtLink>
              </li>
            </ul>
          </section>
        </div>
      </template>

      <p v-else class="text-center text-ink-muted">{{ t("search.empty", { query }) }}</p>
    </div>
  </div>
</template>
