<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015).
definePageMeta({ layout: "studio" });

import { DbButton, DbChip, DbInput } from "@databro/ui";
import { ApiClientError, type ApiClient } from "@databro/api-client";
import type { AuthoringQuiz, AuthoringQuizQuestion } from "@databro/types";

const { t } = useI18n();

/**
 * Quiz builder.
 *
 * Every mutation returns the whole quiz, so this replaces its state rather than patching — the same
 * contract the course and path builders rely on, and the reason reordering never has to be
 * reimplemented here.
 *
 * The answer key is set **as a whole** (AS-4's sibling rule): the API refuses two correct choices on
 * a single-choice question, so the control is a radio group there and checkboxes only where several
 * answers are genuinely allowed. The invalid state is unreachable rather than validated.
 */
const route = useRoute();
const { withAuth } = useAuth();

const quizId = computed(() => String(route.params.id));

const { data: loaded } = await useAsyncData(
  () => `authoring:quiz:${quizId.value}`,
  () => withAuth((api) => api.getAuthoringQuiz(quizId.value)),
  { watch: [quizId] },
);

const quiz = ref<AuthoringQuiz | null>(loaded.value ?? null);

const title = ref(quiz.value?.title ?? "");
const passingScore = ref(quiz.value?.passingScore ?? 70);

const busy = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);

const newPrompt = ref("");
const newType = ref<"singlechoice" | "multiplechoice" | "truefalse">("singlechoice");
const newChoiceText = reactive<Record<string, string>>({});

const questions = computed(() => quiz.value?.questions ?? []);
const isPublished = computed(() => quiz.value?.status === "published");

function describe(error: unknown) {
  if (error instanceof ApiClientError) return error.message;
  return t("studio.common.genericError");
}

async function run(action: (api: ApiClient) => Promise<AuthoringQuiz>) {
  formError.value = null;
  busy.value = true;

  try {
    quiz.value = await withAuth(action);
    savedAt.value = new Date().toLocaleTimeString();
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

function addQuestion() {
  if (!newPrompt.value.trim()) return;

  const prompt = newPrompt.value;
  const type = newType.value;
  newPrompt.value = "";

  return run((api) => api.addQuestion(quizId.value, { prompt, type, points: 1 }));
}

function addChoice(questionId: string) {
  const text = (newChoiceText[questionId] ?? "").trim();
  if (!text) return;

  newChoiceText[questionId] = "";
  return run((api) => api.addChoice(quizId.value, questionId, text));
}

/** Single-choice: the selection replaces the key outright. */
function setSingleAnswer(question: AuthoringQuizQuestion, choiceId: string) {
  return run((api) => api.setCorrectChoices(quizId.value, question.id, [choiceId]));
}

/** Multiple-choice: toggle one, then send the whole resulting set. */
function toggleAnswer(question: AuthoringQuizQuestion, choiceId: string) {
  const current = question.choices.filter((c) => c.isCorrect).map((c) => c.id);
  const next = current.includes(choiceId)
    ? current.filter((id) => id !== choiceId)
    : [...current, choiceId];

  // The API refuses an empty key, so a last-answer untick is stopped here rather than round-tripped
  // into an error the author cannot act on.
  if (next.length === 0) {
    formError.value = t("studio.quizzes.atLeastOneCorrect");
    return;
  }

  return run((api) => api.setCorrectChoices(quizId.value, question.id, next));
}

/** Whether a question would block publishing, shown inline so the author sees it before trying. */
function problemWith(question: AuthoringQuizQuestion) {
  if (question.choices.length < 2) return t("studio.quizzes.needsTwoChoices");
  if (!question.choices.some((c) => c.isCorrect)) return t("studio.quizzes.needsCorrect");
  return null;
}

const blockers = computed(() => questions.value.filter((q) => problemWith(q) !== null).length);

useHead(() => ({ title: title.value || t("studio.quizzes.docTitle") }));
</script>

<template>
  <div v-if="quiz" class="mx-auto max-w-3xl">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <NuxtLink to="/studio/quizzes" class="text-sm font-medium text-accent-strong hover:underline">
        ← Quizzes
      </NuxtLink>

      <div class="flex items-center gap-3">
        <span v-if="savedAt" class="text-xs text-ink-subtle">{{ t("studio.common.savedAt", { time: savedAt }) }}</span>
        <NuxtLink
          :to="`/studio/quizzes/${quizId}/attempts`"
          class="text-sm font-medium text-accent-strong hover:underline"
        >
          {{ t("studio.quizzes.attempts") }}
        </NuxtLink>
        <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ quiz.status }}</DbChip>
      </div>
    </div>

    <p
      v-if="formError"
      role="alert"
      class="mt-4 rounded-control border border-danger/30 bg-danger-subtle px-3 py-2 text-sm text-danger"
    >
      {{ formError }}
    </p>

    <section class="mt-6 rounded-card border border-line bg-surface-raised p-6">
      <h1 class="font-display text-xl font-bold tracking-tight text-ink">{{ t("studio.quizzes.details") }}</h1>

      <div class="mt-5 space-y-4">
        <DbInput v-model="title" :label="t('studio.common.title')" required :disabled="busy" />
        <label class="block">
          <span class="mb-1.5 block text-sm font-medium text-ink">{{ t("studio.quizzes.passMark") }}</span>
          <input
            v-model.number="passingScore"
            type="number"
            min="0"
            max="100"
            :disabled="busy"
            class="h-10 w-32 rounded-control border border-line-strong bg-surface-raised px-3 text-sm tabular-nums text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
          />
        </label>
      </div>

      <DbButton
        class="mt-5"
        :disabled="busy || !title"
        @click="run((api) => api.updateQuiz(quizId, { title, passingScore }))"
      >
        {{ t("studio.quizzes.saveDetails") }}
      </DbButton>
    </section>

    <section class="mt-6 rounded-card border border-line bg-surface-raised p-6">
      <div class="flex flex-wrap items-baseline justify-between gap-3">
        <h2 class="font-display text-lg font-bold tracking-tight text-ink">{{ t("studio.quizzes.questions") }}</h2>
        <span class="text-sm text-ink-subtle">{{ quiz.totalPoints }} points total</span>
      </div>

      <ol v-if="questions.length" class="mt-5 space-y-5">
        <li
          v-for="(question, index) in questions"
          :key="question.id"
          class="rounded-control border border-line p-4"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0 flex-1">
              <p class="font-medium text-ink">{{ index + 1 }}. {{ question.prompt }}</p>
              <p class="mt-0.5 text-xs text-ink-subtle">
                {{ question.type }} · {{ question.points }} point{{ question.points === 1 ? "" : "s" }}
              </p>
            </div>

            <button
              type="button"
              :disabled="busy"
              class="rounded-control px-2 py-1 text-sm text-danger hover:bg-danger-subtle disabled:opacity-30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
              @click="run((api) => api.removeQuestion(quizId, question.id))"
            >
              {{ t("studio.common.remove") }}
            </button>
          </div>

          <!-- Shown before publishing is attempted, because "publish" failing with a list of
               reasons is a worse way to learn which question is unfinished. -->
          <p v-if="problemWith(question)" class="mt-2 text-sm text-warning">
            {{ problemWith(question) }}
          </p>

          <ul class="mt-3 space-y-2">
            <li v-for="choice in question.choices" :key="choice.id" class="flex items-center gap-3">
              <!-- The checkbox keeps Tailwind's 4px `rounded`, deliberately off the 12/16/24 scale:
                   `rounded-control` on a 16px box is nearly a circle, which is what a radio button
                   looks like. Shape is the only thing separating the two controls here. -->
              <input
                v-if="question.type === 'multiplechoice'"
                type="checkbox"
                :checked="choice.isCorrect"
                :disabled="busy"
                :aria-label="t('studio.quizzes.choiceCorrect', { choice: choice.text })"
                class="h-4 w-4 rounded border-line-strong text-accent-strong focus:ring-accent-strong"
                @change="toggleAnswer(question, choice.id)"
              />
              <input
                v-else
                type="radio"
                :name="`answer-${question.id}`"
                :checked="choice.isCorrect"
                :disabled="busy"
                :aria-label="t('studio.quizzes.choiceCorrect', { choice: choice.text })"
                class="h-4 w-4 border-line-strong text-accent-strong focus:ring-accent-strong"
                @change="setSingleAnswer(question, choice.id)"
              />

              <span class="min-w-0 flex-1 text-sm" :class="choice.isCorrect ? 'font-medium text-ink' : 'text-ink-muted'">
                {{ choice.text }}
              </span>

              <!-- True/false owns its two choices, so they cannot be removed. -->
              <button
                v-if="question.type !== 'truefalse'"
                type="button"
                :disabled="busy"
                :aria-label="t('studio.quizzes.removeChoice', { choice: choice.text })"
                class="rounded-control px-2 py-0.5 text-xs text-ink-subtle hover:bg-surface-sunken hover:text-danger disabled:opacity-30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
                @click="run((api) => api.removeChoice(quizId, question.id, choice.id))"
              >
                ✕
              </button>
            </li>
          </ul>

          <div v-if="question.type !== 'truefalse'" class="mt-3 flex flex-wrap items-center gap-2">
            <input
              v-model="newChoiceText[question.id]"
              type="text"
              :placeholder="t('studio.quizzes.addChoice')"
              :disabled="busy"
              class="h-9 min-w-48 flex-1 rounded-control border border-line-strong bg-surface-raised px-3 text-sm text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
              @keyup.enter="addChoice(question.id)"
            />
            <DbButton
              variant="outline"
              size="sm"
              :disabled="busy || !newChoiceText[question.id]"
              @click="addChoice(question.id)"
            >
              Add
            </DbButton>
          </div>
        </li>
      </ol>

      <p v-else class="mt-5 rounded-control border border-dashed border-line-strong p-6 text-center text-sm text-ink-muted">
        {{ t("studio.quizzes.noQuestions") }}
      </p>

      <div class="mt-5 flex flex-wrap items-center gap-2 border-t border-line pt-5">
        <input
          v-model="newPrompt"
          type="text"
          :placeholder="t('studio.quizzes.newQuestion')"
          :disabled="busy"
          class="h-10 min-w-56 flex-1 rounded-control border border-line-strong bg-surface-raised px-3 text-sm text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
          @keyup.enter="addQuestion"
        />
        <select
          v-model="newType"
          :disabled="busy"
          :aria-label="t('studio.quizzes.questionType')"
          class="h-10 rounded-control border border-line-strong bg-surface-raised px-3 text-sm text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
        >
          <option value="singlechoice">{{ t("studio.quizzes.typeSingle") }}</option>
          <option value="multiplechoice">{{ t("studio.quizzes.typeMultiple") }}</option>
          <option value="truefalse">{{ t("studio.quizzes.typeTrueFalse") }}</option>
        </select>
        <DbButton :disabled="busy || !newPrompt" @click="addQuestion">{{ t("studio.quizzes.addQuestion") }}</DbButton>
      </div>
    </section>

    <section class="mt-6 rounded-card border border-line bg-surface-raised p-6">
      <h2 class="font-display text-lg font-bold tracking-tight text-ink">{{ t("studio.common.publishing") }}</h2>
      <p class="mt-1 text-sm text-ink-muted">
        {{ t("studio.quizzes.publishHint") }}
      </p>

      <p v-if="blockers > 0" class="mt-3 text-sm text-warning">
        {{ t("studio.quizzes.blockersRemain", { count: blockers }) }}
      </p>

      <div class="mt-4 flex flex-wrap gap-3">
        <DbButton
          :disabled="busy || isPublished || questions.length === 0 || blockers > 0"
          @click="run((api) => api.publishQuiz(quizId))"
        >
          {{ t("studio.common.publish") }}
        </DbButton>
        <DbButton
          variant="outline"
          :disabled="busy || !isPublished"
          @click="run((api) => api.unpublishQuiz(quizId))"
        >
          {{ t("studio.common.unpublish") }}
        </DbButton>
      </div>
    </section>
  </div>
</template>
