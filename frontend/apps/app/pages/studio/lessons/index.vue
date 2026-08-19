<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip } from "@databro/ui";
import type { LessonContentSummary, Paged } from "@databro/types";

const { t } = useI18n();

const NuxtLink = resolveComponent("NuxtLink");

/**
 * Lesson bodies. A flat library rather than a tree: a body exists independently of any curriculum,
 * and the same one can sit in more than one course.
 */
const route = useRoute();
const { withAuth } = useAuth();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data } = await useAsyncData<Paged<LessonContentSummary>>(
  () => `authoring:lessons:${page.value}`,
  () => withAuth((api) => api.listLessonContent({ page: page.value, pageSize: 20 })),
  { watch: [page] },
);

const lessons = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

const formatDate = (value?: string) =>
  value ? new Date(value).toLocaleDateString("en", { year: "numeric", month: "short", day: "numeric" }) : "—";

useHead(() => ({ title: t("studio.lessons.navTitle") }));
</script>

<template>
  <div>
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div>
        <h1 class="font-display text-2xl font-bold tracking-tight text-ink">{{ t("studio.lessons.navTitle") }}</h1>
        <p class="mt-1 text-sm text-ink-muted">
          {{ t("studio.lessons.subtitleFull") }}
        </p>
      </div>
      <DbButton :as="NuxtLink" to="/studio/lessons/new">{{ t("studio.lessons.new") }}</DbButton>
    </div>

    <p
      v-if="lessons.length === 0"
      class="mt-8 rounded-card border border-dashed border-line-strong p-10 text-center text-sm text-ink-muted"
    >
      {{ t("studio.lessons.empty") }}
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-accent-deep text-white">
          <tr>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.title") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.status") }}</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">{{ t("studio.lessons.colReadingTime") }}</th>
            <th scope="col" class="px-4 py-3 font-semibold">{{ t("studio.common.published") }}</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line bg-surface">
          <tr v-for="lesson in lessons" :key="lesson.id" class="hover:bg-surface-sunken">
            <td class="px-4 py-3">
              <NuxtLink :to="`/studio/lessons/${lesson.id}`" class="font-medium text-accent hover:underline">
                {{ lesson.title }}
              </NuxtLink>
              <p class="truncate text-xs text-ink-subtle">{{ lesson.slug }}</p>
            </td>
            <td class="px-4 py-3">
              <DbChip :tone="lesson.status === 'published' ? 'success' : 'neutral'">
                {{ lesson.status }}
              </DbChip>
            </td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">
              {{ lesson.readingTimeMinutes }} min
            </td>
            <td class="px-4 py-3 text-ink-muted">{{ formatDate(lesson.publishedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="meta && meta.totalPages > 1" class="mt-4 text-sm text-ink-muted">
      {{ t("studio.common.pageOf", { page: meta.page, pages: meta.totalPages, total: meta.total, noun: t("studio.lessons.noun") }) }}
    </p>
  </div>
</template>
