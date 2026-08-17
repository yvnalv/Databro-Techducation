<script setup lang="ts">
import { DbChip } from "@databro/ui";
import type { CourseSummary, Paged } from "@databro/types";

/**
 * The course catalogue.
 *
 * Indexable and offset-paged like the article listings — a crawler cannot press a button, and these
 * pages are how a course is discovered before search knows about it.
 */
const { t } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data, error } = await useAsyncData<Paged<CourseSummary>>(
  () => `courses:${page.value}`,
  () => client.listCourses({ page: page.value }).catch((cause) => { throw toNuxtError(cause); }),
  { watch: [page] },
);

const courses = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

if (meta.value) assertPageInRange(meta.value);

useListingSeo({
  title: t("courses.listTitle"),
  description: t("courses.listDescription"),
  path: "/courses",
  meta: meta.value ?? { page: 1, pageSize: 20, total: 0, totalPages: 1 },
});

/** Minutes read badly past an hour, and a course length is the first thing a learner weighs. */
function duration(minutes: number) {
  if (minutes <= 0) return null;
  if (minutes < 60) return t("courses.minutes", { count: minutes });

  const hours = Math.round(minutes / 60);
  return t("courses.hours", { count: hours });
}
</script>

<template>
  <div>
    <PageHeader
      :eyebrow="t('courses.eyebrow')"
      :title="t('courses.listTitle')"
      :subtitle="t('courses.listDescription')"
    />

    <div class="db-shell py-14 sm:py-20">
      <p v-if="error" class="text-center text-ink-muted">{{ t("courses.unavailable") }}</p>

      <p v-else-if="courses.length === 0" class="text-center text-ink-muted">
        {{ t("courses.listEmpty") }}
      </p>

      <ul v-else class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <li v-for="course in courses" :key="course.id">
          <NuxtLink
            :to="localePath(`/courses/${course.slug}`)"
            class="flex h-full flex-col rounded-card border border-line bg-surface p-6 transition-shadow hover:shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          >
            <div class="flex flex-wrap items-center gap-2">
              <DbChip tone="info">{{ t(`courses.difficulty.${course.difficulty}`) }}</DbChip>
              <span v-if="duration(course.estimatedMinutes)" class="text-xs text-ink-subtle">
                {{ duration(course.estimatedMinutes) }}
              </span>
            </div>

            <h2 class="mt-3 font-display text-lg font-bold tracking-tight text-ink">
              {{ course.title }}
            </h2>
            <p class="mt-2 flex-1 text-sm leading-relaxed text-ink-muted">{{ course.summary }}</p>

            <p class="mt-4 text-sm font-medium text-accent">
              {{ t("courses.lessonCount", course.lessonCount) }}
            </p>
          </NuxtLink>
        </li>
      </ul>

      <PaginationNav v-if="meta" :meta="meta" base-path="/courses" />
    </div>
  </div>
</template>
