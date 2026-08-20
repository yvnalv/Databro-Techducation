<script setup lang="ts">
import { DbChip } from "@databro/ui";
import type { LearningPath, Paged } from "@databro/types";

/**
 * The learning-path catalogue.
 *
 * A path is the largest commitment the platform offers — several courses in a curated order — so
 * these pages sit above courses in the sitemap and are the ones a broad query like "learn LLM
 * engineering" should land on.
 */
const { t } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data, error } = await useAsyncData<Paged<LearningPath>>(
  () => `paths:${page.value}`,
  () => client.listLearningPaths({ page: page.value }).catch((cause) => { throw toNuxtError(cause); }),
  { watch: [page] },
);

const paths = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

if (meta.value) assertPageInRange(meta.value);

useListingSeo({
  title: t("paths.listTitle"),
  description: t("paths.listDescription"),
  path: "/learning-paths",
  meta: meta.value ?? { page: 1, pageSize: 20, total: 0, totalPages: 1 },
});
</script>

<template>
  <div>
    <PageHeader :eyebrow="t('paths.eyebrow')" :title="t('paths.listTitle')">
      <template #meta>
        <p class="mx-auto mt-4 max-w-2xl text-ink-on-deep/80">{{ t("paths.listDescription") }}</p>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <p v-if="error" class="text-center text-ink-muted">{{ t("paths.loadFailed") }}</p>

      <ul v-else-if="paths.length" class="grid gap-6 sm:grid-cols-2">
        <li v-for="path in paths" :key="path.id">
          <NuxtLink
            :to="localePath(`/learning-paths/${path.slug}`)"
            class="group flex h-full flex-col rounded-card border border-line bg-surface p-6 transition-shadow hover:shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
          >
            <div class="flex flex-wrap items-center gap-2">
              <DbChip tone="neutral">{{ t(`courses.difficulty.${path.difficulty}`) }}</DbChip>
              <span class="text-sm text-ink-subtle">
                {{ t("paths.courseCount", path.courses.length) }}
              </span>
            </div>

            <h2 class="mt-3 font-display text-xl font-bold tracking-tight text-ink group-hover:text-accent-strong">
              {{ path.title }}
            </h2>
            <p v-if="path.summary" class="mt-2 flex-1 text-sm leading-relaxed text-ink-muted">
              {{ path.summary }}
            </p>
          </NuxtLink>
        </li>
      </ul>

      <p v-else class="text-center text-ink-muted">{{ t("paths.empty") }}</p>

      <PaginationNav v-if="meta && meta.totalPages > 1" :meta="meta" base-path="/learning-paths" />
    </div>
  </div>
</template>
