<script setup lang="ts">
import { DbButton, DbChip } from "@databro/ui";
import type { Enrollment, Paged } from "@databro/types";

/**
 * The learner's dashboard — the root of the authenticated app (ADR-0015).
 *
 * The one surface that has to render LN-6 honestly: a completed course whose curriculum has since
 * grown shows *both* facts — the completion badge and a fraction below 100% — rather than picking
 * whichever is tidier. Hiding either would be the UI quietly disagreeing with the domain.
 */
const { t, locale } = useI18n();
const { withAuth } = useAuth();
const config = useRuntimeConfig();

const publicSiteUrl = computed(() => config.public.siteUrl as string);

const { data, error } = await useAsyncData<Paged<Enrollment>>(
  "me:enrollments",
  () => withAuth((api) => api.listMyEnrollments({ pageSize: 50 })),
);

const enrollments = computed(() => data.value?.items ?? []);

const stats = computed(() => ({
  enrolled: data.value?.meta.total ?? 0,
  completed: enrollments.value.filter((e) => e.completedAt).length,
  inProgress: enrollments.value.filter((e) => !e.completedAt && e.completedLessons > 0).length,
}));

const formatDate = (value?: string | null) =>
  value
    ? new Date(value).toLocaleDateString(locale.value, {
        year: "numeric",
        month: "short",
        day: "numeric",
      })
    : "—";

/**
 * A completed course can legitimately sit below 100% (LN-6). Worth saying out loud on the card:
 * without a word of explanation, "Completed · 1 of 2" looks like a bug in our arithmetic.
 *
 * The wording stays **cause-neutral** on purpose. Two different things produce this state — the
 * course grew after the learner finished it, or they un-ticked a lesson afterwards — and the DTO
 * cannot tell them apart. Naming either one would be right half the time; "your completion stands"
 * is right in both, and is the part the learner actually needs to know.
 */
const isCompleteButPartial = (e: Enrollment) =>
  Boolean(e.completedAt) && e.completedLessons < e.totalLessons;

/** Resume, start, or review — the card's single primary action, named for what it actually does. */
const actionLabel = (e: Enrollment) => {
  if (e.completedAt) return t("dashboard.review");
  return e.lastLessonId ? t("dashboard.resume") : t("dashboard.start");
};

/**
 * Where the card's action goes — the public site, because reading is `site`'s job (ADR-0015).
 *
 * Straight to the lesson they left off on when there is one. `lastLessonSlug` is null when that
 * lesson has since been unpublished or dropped from the curriculum, and the course page is the right
 * fallback: one click away, and unlike the stale lesson URL it exists.
 */
const courseHref = (e: Enrollment) => {
  const base = `${publicSiteUrl.value}/courses/${e.courseSlug}`;
  return e.lastLessonSlug ? `${base}/${e.lastLessonSlug}` : base;
};
</script>

<template>
  <div>
    <header class="mb-8">
      <h1 class="font-display text-2xl font-bold tracking-tight text-ink sm:text-3xl">
        {{ t("dashboard.title") }}
      </h1>
      <p class="mt-1.5 text-sm text-ink-muted">{{ t("dashboard.subtitle") }}</p>
    </header>

    <p v-if="error" class="rounded-card border border-line bg-surface p-6 text-center text-ink-muted">
      {{ t("dashboard.loadFailed") }}
    </p>

    <template v-else-if="enrollments.length">
      <dl class="mb-8 grid grid-cols-3 gap-3 sm:gap-4">
        <div
          v-for="stat in [
            { key: 'enrolled', value: stats.enrolled },
            { key: 'inProgress', value: stats.inProgress },
            { key: 'completed', value: stats.completed },
          ]"
          :key="stat.key"
          class="rounded-card border border-line bg-surface p-4"
        >
          <dt class="text-xs font-medium uppercase tracking-wide text-ink-subtle">
            {{ t(`stats.${stat.key}`) }}
          </dt>
          <dd class="mt-1 font-display text-2xl font-bold text-ink">{{ stat.value }}</dd>
        </div>
      </dl>

      <ul class="space-y-4">
        <li
          v-for="enrollment in enrollments"
          :key="enrollment.id"
          class="rounded-card border border-line bg-surface p-5"
        >
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <h2 class="font-display text-lg font-bold tracking-tight text-ink">
                  {{ enrollment.courseTitle }}
                </h2>
                <DbChip v-if="enrollment.completedAt" tone="success">
                  {{ t("dashboard.completed") }}
                </DbChip>
              </div>

              <p class="mt-1 text-sm text-ink-muted">
                <span v-if="enrollment.completedAt">
                  {{ t("dashboard.completedOn", { date: formatDate(enrollment.completedAt) }) }}
                </span>
                <span v-else-if="enrollment.lastAccessedAt">
                  {{ t("dashboard.lastOpened", { date: formatDate(enrollment.lastAccessedAt) }) }}
                </span>
                <span v-else>{{ t("dashboard.notStarted") }}</span>
              </p>
            </div>

            <!-- A plain anchor, not NuxtLink: the course page is a different origin (`site`), so
                 this is a document navigation and not a router push. -->
            <DbButton as="a" :href="courseHref(enrollment)">
              {{ actionLabel(enrollment) }}
            </DbButton>
          </div>

          <div class="mt-4">
            <div class="flex items-baseline justify-between gap-3 text-sm">
              <span class="text-ink-muted">
                {{
                  t("dashboard.progress", {
                    completed: enrollment.completedLessons,
                    total: enrollment.totalLessons,
                  })
                }}
              </span>
              <span class="font-medium text-ink">
                {{ t("dashboard.percent", { percent: enrollment.percentComplete }) }}
              </span>
            </div>

            <!-- Native semantics rather than a styled div: a progress bar that screen readers
                 cannot announce is decoration, and this is the number the page exists to show. -->
            <div
              class="mt-2 h-2 overflow-hidden rounded-full bg-surface-sunken"
              role="progressbar"
              :aria-valuenow="enrollment.percentComplete"
              aria-valuemin="0"
              aria-valuemax="100"
              :aria-label="enrollment.courseTitle"
            >
              <div
                class="h-full rounded-full bg-accent transition-[width]"
                :style="{ width: `${enrollment.percentComplete}%` }"
              />
            </div>

            <p v-if="isCompleteButPartial(enrollment)" class="mt-2 text-sm text-ink-subtle">
              {{ t("dashboard.completionStands") }}
            </p>
          </div>
        </li>
      </ul>
    </template>

    <div v-else class="rounded-card border border-line bg-surface p-10 text-center">
      <p class="text-ink-muted">{{ t("dashboard.empty") }}</p>
      <DbButton as="a" :href="`${publicSiteUrl}/courses`" class="mt-4">
        {{ t("dashboard.emptyCta") }}
      </DbButton>
    </div>
  </div>
</template>
