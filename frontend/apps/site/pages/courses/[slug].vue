<script setup lang="ts">
import { DbChip } from "@databro/ui";
import type { Course } from "@databro/types";

/**
 * A course page: the curriculum a learner reads before deciding to start.
 *
 * Lessons whose bodies are unpublished are already absent — the API omits them (ADR-0013) — so this
 * page never has to reason about draft content. What arrives is exactly what may be shown.
 */
const { t, locale, locales } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const client = useApiClient();
const config = useRuntimeConfig();
const nuxtApp = useNuxtApp();

const slug = computed(() => String(route.params.slug));

const { data: course, error } = await useAsyncData<Course>(
  () => `course:${slug.value}`,
  () => client.getCourse(slug.value).catch((cause) => { throw toNuxtError(cause); }),
  { watch: [slug] },
);

// An unpublished or missing course must be a real 404, not a 200 with nothing on it — the same rule
// articles follow. A moved slug resolves to its 301 first.
if (error.value || !course.value) {
  await honorRedirect(`/courses/${slug.value}`, { nuxtApp, client, localePath });
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const published = course.value;

const origin = config.public.siteUrl.replace(/\/$/, "");
const canonical = `${origin}${localePath(`/courses/${published.slug}`)}`;

function duration(minutes: number) {
  if (minutes <= 0) return null;
  if (minutes < 60) return t("courses.minutes", { count: minutes });

  return t("courses.hours", { count: Math.round(minutes / 60) });
}

useHead({
  htmlAttrs: { lang: locale.value },
  link: [
    { rel: "canonical", href: canonical },
    ...locales.value.map((l) => {
      const code = typeof l === "string" ? l : l.code;
      return {
        rel: "alternate" as const,
        hreflang: code,
        href: `${origin}${localePath(`/courses/${published.slug}`, code)}`,
        type: "text/html",
      };
    }),
  ],
});

useSeoMeta({
  title: published.title,
  description: published.summary,
  ogType: "website",
  ogTitle: published.title,
  ogDescription: published.summary,
  ogUrl: canonical,
  ogSiteName: "DataBro",
  ogLocale: locale.value,
});

/**
 * `Course` structured data.
 *
 * `hasCourseInstance` is omitted deliberately: it describes a scheduled offering with dates and a
 * mode, and this is self-paced with none of that. Claiming one would be structured data that
 * misrepresents the product — the same reason a flat tag page emits no `BreadcrumbList`.
 */
useHead({
  script: [
    {
      type: "application/ld+json",
      innerHTML: JSON.stringify({
        "@context": "https://schema.org",
        "@type": "Course",
        name: published.title,
        description: published.summary,
        url: canonical,
        inLanguage: locale.value,
        provider: { "@type": "Organization", name: "DataBro", url: origin },
        educationalLevel: published.difficulty,
        ...(published.estimatedMinutes > 0
          // ISO 8601 duration. Minutes rather than hours so a 90-minute course is not rounded into
          // a lie.
          ? { timeRequired: `PT${published.estimatedMinutes}M` }
          : {}),
      }),
    },
  ],
});
</script>

<template>
  <div>
    <PageHeader :eyebrow="t('courses.eyebrow')" :title="published.title" :subtitle="published.summary">
      <template #meta>
        <p class="mt-4 flex flex-wrap items-center justify-center gap-3 text-sm text-white/80">
          <span>{{ t(`courses.difficulty.${published.difficulty}`) }}</span>
          <span aria-hidden="true">·</span>
          <span>{{ t("courses.lessonCount", published.lessonCount) }}</span>
          <template v-if="duration(published.estimatedMinutes)">
            <span aria-hidden="true">·</span>
            <span>{{ duration(published.estimatedMinutes) }}</span>
          </template>
        </p>
      </template>
    </PageHeader>

    <div class="db-shell py-14 sm:py-20">
      <div class="mx-auto max-w-3xl">
        <h2 class="font-display text-2xl font-bold tracking-tight text-ink">
          {{ t("courses.curriculum") }}
        </h2>

        <p v-if="published.modules.length === 0" class="mt-4 text-ink-muted">
          {{ t("courses.curriculumEmpty") }}
        </p>

        <!-- An ordered list because the order is the curriculum: a learner is meant to read these
             in sequence, and a bulleted list would say otherwise. -->
        <ol v-else class="mt-8 space-y-8">
          <li v-for="(module, index) in published.modules" :key="module.id">
            <div class="flex items-baseline gap-3">
              <span class="font-mono text-sm text-ink-subtle tabular-nums">
                {{ String(index + 1).padStart(2, "0") }}
              </span>
              <div class="min-w-0">
                <h3 class="font-display text-lg font-bold tracking-tight text-ink">
                  {{ module.title }}
                </h3>
                <p v-if="module.summary" class="mt-1 text-sm text-ink-muted">{{ module.summary }}</p>
              </div>
            </div>

            <ul class="mt-4 divide-y divide-line rounded-card border border-line">
              <li
                v-for="lesson in module.lessons"
                :key="lesson.id"
                class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3"
              >
                <span class="min-w-0 flex-1 text-ink">{{ lesson.title }}</span>
                <DbChip v-if="lesson.difficulty !== published.difficulty" tone="neutral">
                  {{ t(`courses.difficulty.${lesson.difficulty}`) }}
                </DbChip>
                <span v-if="lesson.estimatedMinutes > 0" class="text-sm tabular-nums text-ink-subtle">
                  {{ t("courses.minutes", { count: lesson.estimatedMinutes }) }}
                </span>
              </li>
            </ul>
          </li>
        </ol>

        <!-- Lesson bodies are not readable yet: reading one needs enrollment and progress, which is
             the next slice. Saying so is better than a link that goes nowhere. -->
        <p class="mt-10 rounded-card border border-line bg-surface-sunken px-5 py-4 text-sm text-ink-muted">
          {{ t("courses.readingComingSoon") }}
        </p>
      </div>
    </div>
  </div>
</template>
