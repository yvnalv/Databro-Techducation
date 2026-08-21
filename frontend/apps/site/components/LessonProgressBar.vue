<script setup lang="ts">
import { DbButton } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";
import type { Enrollment } from "@databro/types";

/**
 * Progress controls on a lesson page.
 *
 * <b>Secondary to the page, by construction.</b> The lesson renders server-side for everyone,
 * including crawlers; this hydrates in afterwards for a signed-in learner. Every failure path here
 * degrades to a sign-in prompt or a quiet no-op — a dead session must never take down the thing the
 * reader actually came for.
 *
 * Signed-out learners see the invitation rather than nothing: it is the honest state, and it is also
 * where enrolments come from.
 */
const props = defineProps<{ courseSlug: string; lessonId: string; returnTo: string }>();

const { t } = useI18n();
const { isSignedIn, tryAuthed, signInUrl } = useLearnerSession();

const enrollment = ref<Enrollment | null>(null);
const expired = ref(false);
const busy = ref(false);
/** Set when a write fails for a reason that is not an expired session. */
const failed = ref(false);
/**
 * Set when completion is refused because this lesson's quiz has not been passed (AS-9). Distinct from
 * a generic failure: it is not an error the learner should retry, it is a step pointing at the quiz
 * above.
 */
const blocked = ref(false);

const isEnrolled = computed(() => enrollment.value !== null);
const isComplete = computed(() =>
  Boolean(enrollment.value?.completedLessonIds.includes(props.lessonId)),
);

/**
 * Loads progress and records the visit in **one** round trip.
 *
 * `visit` returns the whole enrollment, so asking for progress and then reporting arrival would be
 * two calls for one fact. It is also the right moment: the learner has the lesson open, which is
 * exactly what the resume point means (LN-10).
 */
async function load() {
  if (!isSignedIn.value) return;

  const result = await tryAuthed((api) => api.visitLesson(props.courseSlug, props.lessonId));

  if (result.ok) {
    enrollment.value = result.value;
    return;
  }

  expired.value = result.expired;

  // A 404 from `visit` means "not enrolled", which is an ordinary state for someone reading a
  // lesson they have not joined — not an error worth showing.
}

// Client-only: this is per-learner and uncacheable, and running it during SSR would poison the ISR
// cache with one reader's progress served to everyone.
onMounted(load);

async function toggle() {
  if (busy.value) return;
  busy.value = true;
  failed.value = false;
  blocked.value = false;

  const call = isComplete.value
    ? (api: import("@databro/api-client").ApiClient) =>
        api.reopenLesson(props.courseSlug, props.lessonId)
    : (api: import("@databro/api-client").ApiClient) =>
        api.completeLesson(props.courseSlug, props.lessonId);

  try {
    const result = await tryAuthed(call);
    if (result.ok) enrollment.value = result.value;
    else expired.value = result.expired;
  } catch (error) {
    // A 422 on completion is the quiz gate: the learner is enrolled (the button only shows then), so
    // the one rule that refuses a completion here is an unpassed quiz. Anything else is a real
    // failure worth a retry.
    if (error instanceof ApiClientError && error.status === 422) blocked.value = true;
    else failed.value = true;
  } finally {
    busy.value = false;
  }
}

async function join() {
  if (busy.value) return;
  busy.value = true;
  failed.value = false;

  try {
    const result = await tryAuthed((api) => api.enrol(props.courseSlug));
    if (result.ok) enrollment.value = result.value;
    else expired.value = result.expired;
  } catch {
    failed.value = true;
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <div class="rounded-card border border-line bg-surface-sunken p-5">
    <!-- Signed out, or a session that has expired underneath them. Both end in the same place, and
         distinguishing them for the reader would be describing our plumbing. -->
    <div v-if="!isSignedIn || expired" class="flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-ink-muted">{{ t("lesson.signInToTrack") }}</p>
      <DbButton as="a" :href="signInUrl(returnTo)" variant="outline" size="sm">
        {{ t("lesson.signIn") }}
      </DbButton>
    </div>

    <div v-else-if="!isEnrolled" class="flex flex-wrap items-center justify-between gap-3">
      <p class="text-sm text-ink-muted">{{ t("lesson.joinToTrack") }}</p>
      <DbButton size="sm" :disabled="busy" @click="join">{{ t("lesson.join") }}</DbButton>
    </div>

    <div v-else>
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="min-w-0">
          <p class="text-sm font-medium text-ink">
            {{
              t("lesson.courseProgress", {
                completed: enrollment!.completedLessons,
                total: enrollment!.totalLessons,
              })
            }}
          </p>
          <div
            class="mt-2 h-1.5 w-48 overflow-hidden rounded-full bg-surface-raised"
            role="progressbar"
            :aria-valuenow="enrollment!.percentComplete"
            aria-valuemin="0"
            aria-valuemax="100"
            :aria-label="t('lesson.courseProgressLabel')"
          >
            <div
              class="h-full rounded-full bg-accent transition-[width]"
              :style="{ width: `${enrollment!.percentComplete}%` }"
            />
          </div>
        </div>

        <DbButton
          :variant="isComplete ? 'outline' : 'primary'"
          size="sm"
          :disabled="busy"
          @click="toggle"
        >
          {{ isComplete ? t("lesson.markIncomplete") : t("lesson.markComplete") }}
        </DbButton>
      </div>

      <p v-if="blocked" role="alert" class="mt-3 text-sm text-warning">
        {{ t("lesson.completeBlockedByQuiz") }}
      </p>
      <p v-else-if="failed" role="alert" class="mt-3 text-sm text-danger">
        {{ t("lesson.saveFailed") }}
      </p>
    </div>
  </div>
</template>
