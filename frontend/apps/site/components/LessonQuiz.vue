<script setup lang="ts">
import { DbButton } from "@databro/ui";
import type { Quiz, QuizAttempt } from "@databro/types";

/**
 * The quiz on a lesson page.
 *
 * <b>Client-only and secondary, like the progress bar.</b> The lesson renders server-side for
 * everyone including crawlers; this hydrates in for a signed-in learner. A lesson with no quiz, or a
 * reader who is signed out, simply sees nothing extra — never an error, and never a gap where the
 * page used to work.
 *
 * Nothing here can display a correct answer before submission, because nothing here <i>has</i> one:
 * the quiz payload has no correctness field (AS-1) and `attempt.results` is empty until the attempt
 * is closed (AS-2). The guarantee is upstream, in the types.
 */
const props = defineProps<{ lessonId: string; returnTo: string }>();

const { t } = useI18n();
const { isSignedIn, tryAuthed, signInUrl } = useLearnerSession();

const quiz = ref<Quiz | null>(null);
const attempt = ref<QuizAttempt | null>(null);
const expired = ref(false);
const busy = ref(false);
const failed = ref(false);

/** questionId → selected choice ids. */
const selections = reactive<Record<string, string[]>>({});

const submitted = computed(() => Boolean(attempt.value?.submittedAt));
const answeredCount = computed(
  () => Object.values(selections).filter((ids) => ids.length > 0).length,
);

/** Results keyed by question, so the review pass is a lookup rather than a scan per question. */
const resultFor = computed(() => {
  const map = new Map<string, NonNullable<QuizAttempt["results"]>[number]>();
  for (const r of attempt.value?.results ?? []) map.set(r.questionId, r);
  return map;
});

onMounted(async () => {
  if (!isSignedIn.value) {
    // Still worth knowing whether a quiz exists: the sign-in prompt should only appear on lessons
    // that actually have one. Without a session there is no way to ask, so the section stays hidden
    // and the learner discovers it after signing in — the honest trade for not exposing the
    // question bank publicly.
    return;
  }

  const result = await tryAuthed((api) => api.getLessonQuiz(props.lessonId));

  if (result.ok) {
    quiz.value = result.value;
    return;
  }

  // A 404 means this lesson has no quiz, which is the common case and not an error.
  expired.value = result.expired;
});

async function start() {
  if (busy.value) return;
  busy.value = true;
  failed.value = false;

  try {
    const result = await tryAuthed((api) => api.startAttempt(props.lessonId));
    if (result.ok) attempt.value = result.value;
    else expired.value = result.expired;
  } catch {
    failed.value = true;
  } finally {
    busy.value = false;
  }
}

function toggle(questionId: string, choiceId: string, multiple: boolean) {
  const current = selections[questionId] ?? [];

  if (!multiple) {
    selections[questionId] = [choiceId];
    return;
  }

  selections[questionId] = current.includes(choiceId)
    ? current.filter((id) => id !== choiceId)
    : [...current, choiceId];
}

function isSelected(questionId: string, choiceId: string) {
  return (selections[questionId] ?? []).includes(choiceId);
}

async function submit() {
  if (busy.value || !attempt.value) return;
  busy.value = true;
  failed.value = false;

  try {
    const result = await tryAuthed((api) => api.submitAttempt(attempt.value!.id, { ...selections }));
    if (result.ok) attempt.value = result.value;
    else expired.value = result.expired;
  } catch {
    failed.value = true;
  } finally {
    busy.value = false;
  }
}

/** A retake starts a fresh attempt; the previous one is kept (AS-7). */
async function retake() {
  attempt.value = null;
  for (const key of Object.keys(selections)) delete selections[key];
  await start();
}
</script>

<template>
  <!-- Rendered only when this lesson actually has a quiz. A heading over nothing is noise. -->
  <section v-if="quiz" class="rounded-card border border-line bg-surface p-6">
    <div class="flex flex-wrap items-baseline justify-between gap-3">
      <h2 class="font-display text-xl font-bold tracking-tight text-ink">{{ quiz.title }}</h2>
      <span class="text-sm text-ink-subtle">
        {{ t("quiz.pointsAndPass", { points: quiz.totalPoints, pass: quiz.passingScore }) }}
      </span>
    </div>

    <div v-if="!isSignedIn || expired" class="mt-4 flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-ink-muted">{{ t("quiz.signInToTake") }}</p>
      <DbButton as="a" :href="signInUrl(returnTo)" variant="outline" size="sm">
        {{ t("lesson.signIn") }}
      </DbButton>
    </div>

    <div v-else-if="!attempt" class="mt-4 flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-ink-muted">{{ t("quiz.intro", { count: quiz.questions.length }) }}</p>
      <DbButton size="sm" :disabled="busy" @click="start">{{ t("quiz.start") }}</DbButton>
    </div>

    <template v-else>
      <!-- The score banner, once there is one. -->
      <div
        v-if="submitted"
        class="mt-4 rounded-md border px-4 py-3"
        :class="attempt.passed
          ? 'border-success/30 bg-success-subtle text-success'
          : 'border-warning/30 bg-warning-subtle text-warning'"
      >
        <p class="font-medium">
          {{ attempt.passed ? t("quiz.passed") : t("quiz.notPassed") }} ·
          {{ t("quiz.scoreLine", {
            score: attempt.score,
            total: attempt.totalPoints,
            percent: attempt.percentage,
          }) }}
        </p>
      </div>

      <ol class="mt-5 space-y-6">
        <li v-for="(question, index) in quiz.questions" :key="question.id">
          <p class="font-medium text-ink">{{ index + 1 }}. {{ question.prompt }}</p>
          <p v-if="question.type === 'multiplechoice'" class="mt-0.5 text-xs text-ink-subtle">
            {{ t("quiz.selectAll") }}
          </p>

          <ul class="mt-3 space-y-2">
            <li v-for="choice in question.choices" :key="choice.id">
              <label
                class="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 text-sm transition-colors"
                :class="[
                  submitted && resultFor.get(question.id)?.correctChoiceIds.includes(choice.id)
                    ? 'border-success/40 bg-success-subtle'
                    : submitted && isSelected(question.id, choice.id)
                      ? 'border-danger/40 bg-danger-subtle'
                      : 'border-line hover:bg-surface-sunken',
                ]"
              >
                <input
                  :type="question.type === 'multiplechoice' ? 'checkbox' : 'radio'"
                  :name="`q-${question.id}`"
                  :checked="isSelected(question.id, choice.id)"
                  :disabled="submitted || busy"
                  class="h-4 w-4 border-line-strong text-accent focus:ring-accent"
                  @change="toggle(question.id, choice.id, question.type === 'multiplechoice')"
                />
                <span class="text-ink">{{ choice.text }}</span>
              </label>
            </li>
          </ul>

          <!-- Only ever populated after submission, because the API sends nothing before it. -->
          <p
            v-if="submitted && resultFor.get(question.id)?.explanation"
            class="mt-2 text-sm leading-relaxed text-ink-muted"
          >
            {{ resultFor.get(question.id)?.explanation }}
          </p>
        </li>
      </ol>

      <p v-if="failed" role="alert" class="mt-4 text-sm text-danger">{{ t("quiz.saveFailed") }}</p>

      <div class="mt-6 flex flex-wrap items-center gap-3">
        <DbButton
          v-if="!submitted"
          :disabled="busy || answeredCount === 0"
          @click="submit"
        >
          {{ t("quiz.submit") }}
        </DbButton>
        <DbButton v-else variant="outline" :disabled="busy" @click="retake">
          {{ t("quiz.retake") }}
        </DbButton>

        <span v-if="!submitted" class="text-sm text-ink-subtle">
          {{ t("quiz.answered", { answered: answeredCount, total: quiz.questions.length }) }}
        </span>
      </div>
    </template>
  </section>
</template>
