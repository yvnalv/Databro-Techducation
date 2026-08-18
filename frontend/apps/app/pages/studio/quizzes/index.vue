<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015).
definePageMeta({ layout: "studio" });

import { DbChip } from "@databro/ui";
import type { AuthoringQuiz, Paged } from "@databro/types";

/**
 * Quiz list.
 *
 * There is no "New quiz" button here on purpose: a quiz belongs to a lesson (AS-6), so it is created
 * from the lesson it assesses rather than from a list that would have to ask which one.
 */
const route = useRoute();
const { withAuth } = useAuth();

const page = computed(() => Number(route.query.page ?? 1) || 1);

const { data } = await useAsyncData<Paged<AuthoringQuiz>>(
  () => `authoring:quizzes:${page.value}`,
  () => withAuth((api) => api.listAuthoringQuizzes({ page: page.value, pageSize: 20 })),
  { watch: [page] },
);

const quizzes = computed(() => data.value?.items ?? []);
const meta = computed(() => data.value?.meta);

const STATUS_TONE = {
  published: "success",
  draft: "neutral",
  unpublished: "warning",
} as const;

useHead({ title: "Quizzes" });
</script>

<template>
  <div>
    <div>
      <h1 class="font-display text-2xl font-bold tracking-tight text-ink">Quizzes</h1>
      <p class="mt-1 text-sm text-ink-muted">
        One quiz per lesson. Create a quiz from its lesson, then write the questions here.
      </p>
    </div>

    <p
      v-if="quizzes.length === 0"
      class="mt-8 rounded-card border border-dashed border-line-strong p-10 text-center text-sm text-ink-muted"
    >
      No quizzes yet. Open a lesson and add one.
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-accent-deep text-white">
          <tr>
            <th scope="col" class="px-4 py-3 font-semibold">Title</th>
            <th scope="col" class="px-4 py-3 font-semibold">Status</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">Questions</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">Points</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">Pass mark</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line bg-surface">
          <tr v-for="quiz in quizzes" :key="quiz.id" class="hover:bg-surface-sunken">
            <td class="px-4 py-3">
              <NuxtLink :to="`/studio/quizzes/${quiz.id}`" class="font-medium text-accent hover:underline">
                {{ quiz.title }}
              </NuxtLink>
            </td>
            <td class="px-4 py-3"><DbChip :tone="STATUS_TONE[quiz.status]">{{ quiz.status }}</DbChip></td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ quiz.questions.length }}</td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ quiz.totalPoints }}</td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ quiz.passingScore }}%</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="meta && meta.totalPages > 1" class="mt-4 text-sm text-ink-muted">
      Page {{ meta.page }} of {{ meta.totalPages }} · {{ meta.total }} quizzes
    </p>
  </div>
</template>
