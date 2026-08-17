<script setup lang="ts">
import { DbChip } from "@databro/ui";
import type { LearningPath } from "@databro/types";

/**
 * A learning path: the curated sequence, as a numbered route through several courses.
 *
 * Courses that are not published are already absent — the API drops them, the same way a course
 * drops an unpublished lesson — so this page never reasons about unready content. What arrives is
 * exactly what may be shown.
 */
const { t, locale } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();
const config = useRuntimeConfig();

const slug = computed(() => String(route.params.slug));

const { data, error } = await useAsyncData<LearningPath>(
  () => `path:${slug.value}`,
  () => client.getLearningPath(slug.value).catch((cause) => { throw toNuxtError(cause); }),
  { watch: [slug] },
);

if (error.value || !data.value) {
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const path = computed(() => data.value!);

const origin = config.public.siteUrl.replace(/\/$/, "");
const canonical = computed(() => `${origin}${localePath(`/learning-paths/${path.value.slug}`)}`);

/** Summed across the path's courses — the number a learner actually weighs before starting. */
const totalMinutes = computed(() =>
  path.value.courses.reduce((sum, course) => sum + course.estimatedMinutes, 0),
);

const totalLessons = computed(() =>
  path.value.courses.reduce((sum, course) => sum + course.lessonCount, 0),
);

function duration(minutes: number) {
  if (minutes <= 0) return null;
  if (minutes < 60) return t("courses.minutes", { count: minutes });

  return t("courses.hours", { count: Math.round(minutes / 60) });
}

useSeoMeta({
  title: path.value.title,
  description: path.value.summary,
  ogTitle: path.value.title,
  ogDescription: path.value.summary,
});

useHead(() => ({
  link: [{ rel: "canonical", href: canonical.value }],
  script: [
    {
      type: "application/ld+json",
      // An `ItemList` of Courses rather than a Course: a path is a curated route through several
      // courses, and claiming it is one course would misdescribe both its size and its parts.
      innerHTML: JSON.stringify({
        "@context": "https://schema.org",
        "@type": "ItemList",
        name: path.value.title,
        description: path.value.summary,
        url: canonical.value,
        inLanguage: locale.value,
        numberOfItems: path.value.courses.length,
        itemListElement: path.value.courses.map((course, index) => ({
          "@type": "ListItem",
          position: index + 1,
          item: {
            "@type": "Course",
            name: course.title,
            description: course.summary,
            url: `${origin}${localePath(`/courses/${course.slug}`)}`,
          },
        })),
      }),
    },
  ],
}));
</script>

<template>
  <div>
    <PageHeader :eyebrow="t('paths.eyebrow')" :title="path.title">
      <template #meta>
        <p v-if="path.summary" class="mx-auto mt-4 max-w-2xl text-white/80">{{ path.summary }}</p>
        <div class="mt-5 flex flex-wrap items-center justify-center gap-x-5 gap-y-2 text-sm text-white/75">
          <span>{{ t("paths.courseCount", path.courses.length) }}</span>
          <span>{{ t("courses.lessonCount", totalLessons) }}</span>
          <span v-if="duration(totalMinutes)">{{ duration(totalMinutes) }}</span>
        </div>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <div class="mx-auto max-w-3xl">
        <h2 class="font-display text-xl font-bold tracking-tight text-ink">
          {{ t("paths.sequence") }}
        </h2>
        <p class="mt-1.5 text-sm text-ink-muted">{{ t("paths.sequenceHint") }}</p>

        <!-- Numbered, because the order is the entire point of a path. An unordered list of the
             same courses would be the catalogue with a title on it. -->
        <ol class="mt-6 space-y-4">
          <li v-for="(course, index) in path.courses" :key="course.id">
            <NuxtLink
              :to="localePath(`/courses/${course.slug}`)"
              class="group flex gap-4 rounded-card border border-line bg-surface p-5 transition-shadow hover:shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            >
              <span
                class="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent-subtle font-display text-sm font-bold text-accent"
                aria-hidden="true"
              >
                {{ index + 1 }}
              </span>

              <span class="min-w-0 flex-1">
                <span class="block font-display text-lg font-bold tracking-tight text-ink group-hover:text-accent">
                  {{ course.title }}
                </span>
                <span v-if="course.summary" class="mt-1 block text-sm leading-relaxed text-ink-muted">
                  {{ course.summary }}
                </span>
                <span class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-subtle">
                  <DbChip tone="neutral">{{ t(`courses.difficulty.${course.difficulty}`) }}</DbChip>
                  <span>{{ t("courses.lessonCount", course.lessonCount) }}</span>
                  <span v-if="duration(course.estimatedMinutes)">
                    {{ duration(course.estimatedMinutes) }}
                  </span>
                </span>
              </span>
            </NuxtLink>
          </li>
        </ol>

        <p v-if="!path.courses.length" class="mt-6 rounded-card border border-line bg-surface-sunken px-5 py-4 text-sm text-ink-muted">
          {{ t("paths.noCoursesYet") }}
        </p>
      </div>
    </div>
  </div>
</template>
