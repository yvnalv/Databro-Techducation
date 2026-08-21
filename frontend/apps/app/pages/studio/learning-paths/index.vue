<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip } from "@databro/ui";
import type { LearningPath, Paged } from "@databro/types";

const { t } = useI18n();

const NuxtLink = resolveComponent("NuxtLink");

/**
 * Learning-path list. Reads the authoring endpoint, so drafts are here — the public listing serves
 * published paths only.
 */
const route = useRoute();
const { withAuth } = useAuth();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data } = await useAsyncData<Paged<LearningPath>>(
  () => `authoring:paths:${page.value}`,
  () => withAuth((api) => api.listAuthoringLearningPaths({ page: page.value, pageSize: 20 })),
  { watch: [page] },
);

const paths = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

const STATUS_TONE = {
  published: "success",
  draft: "neutral",
  unpublished: "warning",
} as const;

useHead(() => ({ title: t("studio.paths.navTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="font-display text-2xl font-bold tracking-tight text-ink">{{ t("studio.paths.navTitle") }}</h1>
        <p class="mt-1 text-sm text-ink-muted">
          {{ t("studio.paths.subtitle") }}
        </p>
      </div>
      <DbButton :as="NuxtLink" to="/studio/learning-paths/new">{{ t("studio.paths.new") }}</DbButton>
    </div>

    <p
      v-if="paths.length === 0"
      class="mt-8 rounded-card border border-dashed border-line-strong p-10 text-center text-sm text-ink-muted"
    >
      {{ t("studio.paths.empty") }}
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-accent-deep text-ink-on-deep">
          <tr>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.title") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.status") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.difficulty") }}</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">{{ t("studio.paths.colCourses") }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line bg-surface-raised">
          <tr v-for="path in paths" :key="path.id" class="hover:bg-surface-sunken">
            <td class="px-4 py-3">
              <NuxtLink
                :to="`/studio/learning-paths/${path.id}`"
                class="font-medium text-accent-strong hover:underline"
              >
                {{ path.title }}
              </NuxtLink>
              <p class="truncate text-xs text-ink-subtle">{{ path.slug }}</p>
            </td>
            <td class="px-4 py-3">
              <DbChip :tone="STATUS_TONE[path.status]">{{ path.status }}</DbChip>
            </td>
            <td class="px-4 py-3 text-ink-muted">{{ path.difficulty }}</td>
            <!-- The authoring read keeps unpublished courses, so this is the real length of the
                 path rather than the shorter thing a learner currently sees. -->
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ path.courses.length }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="meta && meta.totalPages > 1" class="mt-4 text-sm text-ink-muted">
      {{ t("studio.common.pageOf", { page: meta.page, pages: meta.totalPages, total: meta.total, noun: t("studio.paths.noun") }) }}
    </p>
  </div>
</template>
