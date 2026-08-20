<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip } from "@databro/ui";
import type { CourseSummary, Paged } from "@databro/types";

const { t } = useI18n();

const NuxtLink = resolveComponent("NuxtLink");

/**
 * Course list. Reads the authoring endpoint, so every status is here — the public listing serves
 * published courses only.
 */
const route = useRoute();
const { withAuth } = useAuth();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data } = await useAsyncData<Paged<CourseSummary>>(
  () => `authoring:courses:${page.value}`,
  () => withAuth((api) => api.listAuthoringCourses({ page: page.value, pageSize: 20 })),
  { watch: [page] },
);

const courses = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

const STATUS_TONE = {
  published: "success",
  draft: "neutral",
  unpublished: "warning",
} as const;

/** Minutes read better as hours once a course is any real length. */
function duration(minutes: number) {
  if (minutes === 0) return "—";
  if (minutes < 60) return `${minutes} min`;

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} hr` : `${hours} hr ${rest} min`;
}

useHead(() => ({ title: t("studio.courses.navTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="font-display text-2xl font-bold tracking-tight text-ink">{{ t("studio.courses.navTitle") }}</h1>
        <p class="mt-1 text-sm text-ink-muted">
          {{ t("studio.courses.subtitle") }}
        </p>
      </div>
      <DbButton :as="NuxtLink" to="/studio/courses/new">{{ t("studio.courses.new") }}</DbButton>
    </div>

    <p
      v-if="courses.length === 0"
      class="mt-8 rounded-card border border-dashed border-line-strong p-10 text-center text-sm text-ink-muted"
    >
      {{ t("studio.courses.empty") }}
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-accent-deep text-ink-on-deep">
          <tr>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.title") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.status") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.difficulty") }}</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">{{ t("studio.courses.colLessons") }}</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">{{ t("studio.courses.colDuration") }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line bg-surface">
          <tr v-for="course in courses" :key="course.id" class="hover:bg-surface-sunken">
            <td class="px-4 py-3">
              <NuxtLink :to="`/studio/courses/${course.id}`" class="font-medium text-accent-strong hover:underline">
                {{ course.title }}
              </NuxtLink>
              <p class="truncate text-xs text-ink-subtle">{{ course.slug }}</p>
            </td>
            <td class="px-4 py-3">
              <DbChip :tone="STATUS_TONE[course.status]">{{ course.status }}</DbChip>
            </td>
            <td class="px-4 py-3 text-ink-muted">{{ course.difficulty }}</td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ course.lessonCount }}</td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">
              {{ duration(course.estimatedMinutes) }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="meta && meta.totalPages > 1" class="mt-4 text-sm text-ink-muted">
      {{ t("studio.common.pageOf", { page: meta.page, pages: meta.totalPages, total: meta.total, noun: t("studio.courses.noun") }) }}
    </p>
  </div>
</template>
