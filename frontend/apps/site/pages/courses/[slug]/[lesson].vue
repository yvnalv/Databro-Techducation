<script setup lang="ts">
import { ContentRenderer, DbChip, codeHighlighterFor } from "@databro/ui";
import type { LessonPage } from "@databro/types";

/**
 * A lesson page (ADR-0015).
 *
 * Lives on `site`, not in the app, because a lesson is a renderable content unit with SEO value
 * (ADR-0007) — the same reasoning that puts articles here. Progress is layered on top for a
 * signed-in learner and never gates the reading.
 *
 * Nested under the course slug because the course is what gives a lesson prev/next, a breadcrumb
 * and a progress context. The same body reached through two courses is two positions in two
 * sequences, and the URL should say which.
 */
const { t, locale } = useI18n();
const localePath = useLocalePath();
const route = useRoute();
const config = useRuntimeConfig();

const courseSlug = computed(() => String(route.params.slug));
const lessonSlug = computed(() => String(route.params.lesson));

const { data, error } = await useAsyncData(
  () => `lesson:${courseSlug.value}:${lessonSlug.value}`,
  // Through the Nitro route, not the API directly, so highlighting survives a client-side Next
  // click as well as a reload — see server/api/lessons/[course]/[slug].
  () =>
    $fetch<{ page: LessonPage; highlighted: Record<string, string> }>(
      `/api/lessons/${encodeURIComponent(courseSlug.value)}/${encodeURIComponent(lessonSlug.value)}`,
    ).catch((cause) => {
      throw toNuxtError(cause);
    }),
  { watch: [courseSlug, lessonSlug] },
);

// An unpublished course or lesson must be a real 404 — a 200 with nothing on it would leave a dead
// URL indexed. No redirect check: lesson slugs have never been public before today, so there is no
// legacy URL that could have moved.
if (error.value || !data.value) {
  throw toNuxtError(error.value ?? createError({ statusCode: 404 }));
}

const page = computed(() => data.value!.page);
const lesson = computed(() => page.value.lesson);

const highlightCode = computed(() => codeHighlighterFor(data.value!.highlighted));

const path = computed(() => `/courses/${page.value.courseSlug}/${lesson.value.slug}`);
const origin = config.public.siteUrl.replace(/\/$/, "");

useSeoMeta({
  title: lesson.value.title,
  description: lesson.value.summary,
  ogTitle: lesson.value.title,
  ogDescription: lesson.value.summary,
  ogType: "article",
});

useHead(() => ({
  link: [{ rel: "canonical", href: `${origin}${localePath(path.value)}` }],
  script: [
    {
      type: "application/ld+json",
      // `LearningResource` rather than `Article`: this is a lesson inside a course, and saying so
      // is what lets it appear as part of the course in structured results instead of as a stray
      // blog post that happens to mention the same topic.
      innerHTML: JSON.stringify({
        "@context": "https://schema.org",
        "@type": "LearningResource",
        name: lesson.value.title,
        description: lesson.value.summary,
        url: `${origin}${localePath(path.value)}`,
        inLanguage: locale.value,
        position: page.value.position,
        isPartOf: {
          "@type": "Course",
          name: page.value.courseTitle,
          url: `${origin}${localePath(`/courses/${page.value.courseSlug}`)}`,
        },
        ...(lesson.value.objectives.length
          ? { teaches: lesson.value.objectives }
          : {}),
        ...(lesson.value.estimatedMinutes > 0
          ? { timeRequired: `PT${lesson.value.estimatedMinutes}M` }
          : {}),
      }),
    },
  ],
}));
</script>

<template>
  <article class="db-shell py-10 sm:py-14">
    <nav :aria-label="t('lesson.breadcrumbLabel')" class="text-sm text-ink-muted">
      <NuxtLink :to="localePath('/courses')" class="hover:text-ink">
        {{ t("courses.navLabel") }}
      </NuxtLink>
      <span class="mx-2" aria-hidden="true">/</span>
      <NuxtLink :to="localePath(`/courses/${page.courseSlug}`)" class="hover:text-ink">
        {{ page.courseTitle }}
      </NuxtLink>
    </nav>

    <header class="mt-6">
      <p class="text-sm font-medium text-accent-strong">
        {{ page.moduleTitle }} ·
        {{ t("lesson.position", { position: page.position, total: page.totalLessons }) }}
      </p>

      <h1 class="mt-2 font-display text-3xl font-bold tracking-tight text-ink sm:text-4xl">
        {{ lesson.title }}
      </h1>

      <p v-if="lesson.summary" class="mt-3 text-lg leading-relaxed text-ink-muted">
        {{ lesson.summary }}
      </p>

      <div class="mt-4 flex flex-wrap items-center gap-3 text-sm text-ink-subtle">
        <SaveButton kind="lesson" :target-id="lesson.id" />
        <DbChip tone="neutral">{{ t(`courses.difficulty.${lesson.difficulty}`) }}</DbChip>
        <span v-if="lesson.estimatedMinutes > 0">
          {{ t("courses.minutes", { count: lesson.estimatedMinutes }) }}
        </span>
      </div>
    </header>

    <!-- Objectives before the body, because "what you will be able to do" is what tells a learner
         whether to read on. CLAUDE.md requires every lesson to declare them. -->
    <section v-if="lesson.objectives.length" class="mt-8 rounded-card border border-line bg-surface p-5">
      <h2 class="font-display text-base font-bold tracking-tight text-ink">
        {{ t("lesson.objectives") }}
      </h2>
      <ul class="mt-3 list-disc space-y-1.5 ps-5 text-ink-muted">
        <li v-for="(objective, i) in lesson.objectives" :key="i">{{ objective }}</li>
      </ul>
    </section>

    <div class="mt-8">
      <ContentRenderer :document="{ version: 1, blocks: lesson.blocks }" :highlight-code="highlightCode" />
    </div>

    <!-- Between the body and the progress control: check what you read, then mark it done. Renders
         nothing at all when the lesson has no quiz. -->
    <div class="mt-12">
      <LessonQuiz :key="`quiz-${lesson.id}`" :lesson-id="lesson.id" :return-to="path" />
    </div>

    <!-- Below the body: the natural moment to mark something done is after reading it. -->
    <div class="mt-12">
      <LessonProgressBar
        :key="lesson.id"
        :course-slug="page.courseSlug"
        :lesson-id="lesson.id"
        :return-to="path"
      />
    </div>

    <nav :aria-label="t('lesson.pagerLabel')" class="mt-10 grid gap-4 border-t border-line pt-8 sm:grid-cols-2">
      <NuxtLink
        v-if="page.previous"
        :to="localePath(`/courses/${page.courseSlug}/${page.previous.slug}`)"
        class="group rounded-card border border-line bg-surface p-4 transition-shadow hover:shadow-card"
      >
        <span class="text-sm text-ink-subtle">← {{ t("lesson.previous") }}</span>
        <span class="mt-1 block font-medium text-ink group-hover:text-accent-strong">
          {{ page.previous.title }}
        </span>
      </NuxtLink>
      <span v-else />

      <NuxtLink
        v-if="page.next"
        :to="localePath(`/courses/${page.courseSlug}/${page.next.slug}`)"
        class="group rounded-card border border-line bg-surface p-4 text-end transition-shadow hover:shadow-card sm:col-start-2"
      >
        <span class="text-sm text-ink-subtle">{{ t("lesson.next") }} →</span>
        <span class="mt-1 block font-medium text-ink group-hover:text-accent-strong">
          {{ page.next.title }}
        </span>
      </NuxtLink>
    </nav>
  </article>
</template>
