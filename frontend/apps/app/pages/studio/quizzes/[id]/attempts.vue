<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015).
definePageMeta({ layout: "studio" });

import { DbChip } from "@databro/ui";
import type { AuthoringQuiz, QuizAttemptSummary } from "@databro/types";

const { t } = useI18n();

/**
 * Quiz attempt review (U-1).
 *
 * The screen the Assessment module shipped without: an author can write a quiz and a learner can
 * take it, but until now nothing showed who attempted what. A roll-up only — score and pass/fail,
 * never the individual selections. Reviewing outcomes is not the same as re-reading the answer key,
 * and the summary endpoint deliberately does not carry one.
 *
 * Only submitted attempts appear; an in-progress one has no score to review.
 */
const route = useRoute();
const { withAuth } = useAuth();

const quizId = computed(() => String(route.params.id));

const { data: quiz } = await useAsyncData<AuthoringQuiz>(
  () => `authoring:quiz:${quizId.value}`,
  () => withAuth((api) => api.getAuthoringQuiz(quizId.value)),
  { watch: [quizId] },
);

const { data: attempts } = await useAsyncData<QuizAttemptSummary[]>(
  () => `authoring:quiz:${quizId.value}:attempts`,
  () => withAuth((api) => api.listQuizAttempts(quizId.value)),
  { watch: [quizId] },
);

const rows = computed(() => attempts.value ?? []);
const passRate = computed(() => {
  if (rows.value.length === 0) return null;
  return Math.round((rows.value.filter((a) => a.passed).length / rows.value.length) * 100);
});

const dateFmt = new Intl.DateTimeFormat("en", { dateStyle: "medium", timeStyle: "short" });
function when(iso: string) {
  return dateFmt.format(new Date(iso));
}

useHead(() => ({ title: quiz.value ? `Attempts — ${quiz.value.title}` : "Attempts" }));
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <NuxtLink
      :to="`/studio/quizzes/${quizId}`"
      class="text-sm font-medium text-accent-strong hover:underline"
    >
      ← Back to quiz
    </NuxtLink>

    <div class="mt-4 flex flex-wrap items-baseline justify-between gap-3">
      <h1 class="font-display text-2xl font-bold tracking-tight text-ink">
        {{ quiz ? quiz.title : "Quiz" }} — attempts
      </h1>
      <span v-if="passRate !== null" class="text-sm text-ink-subtle">
        {{ rows.length }} submitted · {{ passRate }}% passed
      </span>
    </div>

    <p
      v-if="rows.length === 0"
      class="mt-8 rounded-card border border-dashed border-line-strong p-10 text-center text-sm text-ink-muted"
    >
      {{ t("studio.attempts.empty") }}
    </p>

    <div v-else class="mt-6 overflow-hidden rounded-card border border-line">
      <table class="w-full text-left text-sm">
        <thead class="bg-accent-deep text-ink-on-deep">
          <tr>
            <th scope="col" class="px-4 py-3 font-semibold">Learner</th>
            <th scope="col" class="px-4 py-3 font-semibold">Result</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">Score</th>
            <th scope="col" class="px-4 py-3 text-right font-semibold">Submitted</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-line bg-surface">
          <tr v-for="attempt in rows" :key="attempt.attemptId" class="hover:bg-surface-sunken">
            <td class="px-4 py-3 font-medium text-ink">{{ attempt.learnerName }}</td>
            <td class="px-4 py-3">
              <DbChip :tone="attempt.passed ? 'success' : 'warning'">
                {{ attempt.passed ? "Passed" : "Not passed" }}
              </DbChip>
            </td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">
              {{ attempt.score }} / {{ attempt.totalPoints }} ({{ attempt.percentage }}%)
            </td>
            <td class="px-4 py-3 text-right tabular-nums text-ink-muted">{{ when(attempt.submittedAt) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
