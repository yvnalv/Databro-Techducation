<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { DbButton, DbChip, DbInput } from "@databro/ui";
import { ApiClientError, type ApiClient } from "@databro/api-client";
import type { CourseSummary, Difficulty, LearningPath } from "@databro/types";

const { t } = useI18n();

/**
 * Learning-path builder: the curated sequence.
 *
 * Every mutation returns the whole path, so this page never reassembles state from a partial
 * response — the server is the authority on what the order now is. Reordering renumbers every
 * sibling in the domain, and patching locally would be a second implementation of that invariant.
 */
const route = useRoute();
const router = useRouter();
const { withAuth } = useAuth();
const config = useRuntimeConfig();

const pathId = computed(() => String(route.params.id));
const isNew = computed(() => pathId.value === "new");

const { data: loaded } = await useAsyncData(
  () => `authoring:path:${pathId.value}`,
  async () =>
    isNew.value ? null : await withAuth((api) => api.getAuthoringLearningPath(pathId.value)),
  { watch: [pathId] },
);

// Every course, so the picker can offer the ones not yet in this path. Drafts included on purpose:
// a path is routinely assembled before its courses go live.
const { data: allCourses } = await useAsyncData("authoring:courses:all", () =>
  withAuth((api) => api.listAuthoringCourses({ pageSize: 100 })),
);

const path = ref<LearningPath | null>(loaded.value ?? null);

const title = ref(path.value?.title ?? "");
const summary = ref(path.value?.summary ?? "");
const slug = ref(path.value?.slug ?? "");
const difficulty = ref<Difficulty>(path.value?.difficulty ?? "beginner");

const busy = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);
const picked = ref("");

const status = computed(() => path.value?.status ?? "draft");
const isPublished = computed(() => status.value === "published");
const courses = computed(() => path.value?.courses ?? []);

const publicUrl = computed(
  () => `${String(config.public.siteUrl).replace(/\/$/, "")}/learning-paths/${path.value?.slug}`,
);

/** Courses not already in the path — adding one twice is a no-op server-side, but offering it is noise. */
const available = computed<CourseSummary[]>(() => {
  const inPath = new Set(courses.value.map((c) => c.id));
  return (allCourses.value?.items ?? []).filter((c) => !inPath.has(c.id));
});

function describe(error: unknown) {
  if (error instanceof ApiClientError) return error.message;
  return t("studio.common.genericError");
}

async function run(action: (api: ApiClient) => Promise<LearningPath>) {
  formError.value = null;
  busy.value = true;

  try {
    path.value = await withAuth(action);
    savedAt.value = new Date().toLocaleTimeString();
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

async function saveDetails() {
  if (isNew.value) {
    formError.value = null;
    busy.value = true;

    try {
      const created = await withAuth((api) =>
        api.createLearningPath({
          title: title.value,
          summary: summary.value,
          slug: slug.value || undefined,
          difficulty: difficulty.value,
        }),
      );

      path.value = created;
      // Move off /new so a reload cannot create a second path.
      await router.replace(`/studio/learning-paths/${created.id}`);
    } catch (error) {
      formError.value = describe(error);
    } finally {
      busy.value = false;
    }
    return;
  }

  await run((api) =>
    api.updateLearningPath(pathId.value, {
      title: title.value,
      summary: summary.value,
      difficulty: difficulty.value,
    }),
  );
}

function addPicked() {
  if (!picked.value) return;

  const courseId = picked.value;
  // Cleared before the call so a slow network cannot leave the same course selected and re-added.
  picked.value = "";
  return run((api) => api.addCourseToPath(pathId.value, courseId));
}

/**
 * Moves a course one place, by sending the **whole** new order.
 *
 * One call for a rearrangement rather than a move per row: it is one transaction against one
 * aggregate, and a per-row API would let a drag half-apply.
 */
function move(index: number, delta: number) {
  const next = index + delta;
  if (next < 0 || next >= courses.value.length) return;

  // Splice rather than a destructuring swap: with `noUncheckedIndexedAccess` an index read is
  // `string | undefined`, and splice expresses "take it out, put it back one place over" directly.
  const ids = courses.value.map((c) => c.id);
  const [moved] = ids.splice(index, 1);
  if (moved === undefined) return;
  ids.splice(next, 0, moved);

  return run((api) => api.reorderPathCourses(pathId.value, ids));
}

useHead(() => ({ title: isNew.value ? t("studio.paths.newTitle") : title.value || t("studio.paths.docTitle") }));
</script>

<template>
  <div class="mx-auto max-w-3xl">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <NuxtLink to="/studio/learning-paths" class="text-sm font-medium text-accent-strong hover:underline">
        ← {{ t("studio.paths.back") }}
      </NuxtLink>

      <div class="flex items-center gap-3">
        <span v-if="savedAt" class="text-xs text-ink-subtle">{{ t("studio.common.savedAt", { time: savedAt }) }}</span>
        <DbChip v-if="!isNew" :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
      </div>
    </div>

    <p
      v-if="formError"
      role="alert"
      class="mt-4 rounded-control border border-danger/30 bg-danger-subtle px-3 py-2 text-sm text-danger"
    >
      {{ formError }}
    </p>

    <section class="mt-6 rounded-card border border-line bg-surface p-6">
      <h1 class="font-display text-xl font-bold tracking-tight text-ink">
        {{ isNew ? t("studio.paths.newTitle") : t("studio.paths.editTitle") }}
      </h1>

      <div class="mt-5 space-y-4">
        <DbInput v-model="title" :label="t('studio.common.title')" required :disabled="busy" />
        <DbInput v-model="summary" :label="t('studio.common.summary')" :disabled="busy" />
        <DbInput
          v-if="isNew"
          v-model="slug"
          :label="t('studio.common.slug')"
          :placeholder="t('studio.common.slugHint')"
          :disabled="busy"
        />

        <label class="block">
          <span class="mb-1.5 block text-sm font-medium text-ink">{{ t("studio.common.difficulty") }}</span>
          <select
            v-model="difficulty"
            :disabled="busy"
            class="h-10 w-full rounded-control border border-line-strong bg-surface px-3 text-sm text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
          >
            <option value="beginner">{{ t("studio.common.beginner") }}</option>
            <option value="intermediate">{{ t("studio.common.intermediate") }}</option>
            <option value="advanced">{{ t("studio.common.advanced") }}</option>
          </select>
        </label>
      </div>

      <div class="mt-5 flex flex-wrap items-center gap-3">
        <DbButton :disabled="busy || !title" @click="saveDetails">
          {{ isNew ? t("studio.paths.create") : t("studio.paths.saveDetails") }}
        </DbButton>
        <a
          v-if="isPublished"
          :href="publicUrl"
          target="_blank"
          rel="noopener"
          class="text-sm font-medium text-accent-strong hover:underline"
        >
          {{ t("studio.common.viewPublic") }} ↗
        </a>
      </div>
    </section>

    <!-- The sequence only exists once the path does: a course cannot be attached to something with
         no id yet, and showing a disabled picker would just be a puzzle. -->
    <section v-if="!isNew" class="mt-6 rounded-card border border-line bg-surface p-6">
      <h2 class="font-display text-lg font-bold tracking-tight text-ink">{{ t("studio.paths.sequence") }}</h2>
      <p class="mt-1 text-sm text-ink-muted">
        {{ t("studio.paths.sequenceHint") }}
      </p>

      <ol v-if="courses.length" class="mt-5 space-y-3">
        <li
          v-for="(course, index) in courses"
          :key="course.id"
          class="flex items-center gap-3 rounded-control border border-line px-4 py-3"
        >
          <span
            class="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-accent-subtle text-xs font-semibold text-accent-strong"
            aria-hidden="true"
          >
            {{ index + 1 }}
          </span>

          <span class="min-w-0 flex-1">
            <NuxtLink
              :to="`/studio/courses/${course.id}`"
              class="block truncate font-medium text-accent-strong hover:underline"
            >
              {{ course.title }}
            </NuxtLink>
            <span class="text-xs text-ink-subtle">
              {{ t("studio.paths.lessonsCount", { count: course.lessonCount }) }} · {{ course.difficulty }}
            </span>
          </span>

          <DbChip v-if="course.status !== 'published'" tone="warning">{{ course.status }}</DbChip>

          <span class="flex items-center gap-1">
            <button
              type="button"
              :disabled="busy || index === 0"
              :aria-label="t('studio.paths.moveUp', { title: course.title })"
              class="rounded px-2 py-1 text-sm text-ink-muted hover:bg-surface-sunken disabled:opacity-30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
              @click="move(index, -1)"
            >
              ↑
            </button>
            <button
              type="button"
              :disabled="busy || index === courses.length - 1"
              :aria-label="t('studio.paths.moveDown', { title: course.title })"
              class="rounded px-2 py-1 text-sm text-ink-muted hover:bg-surface-sunken disabled:opacity-30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
              @click="move(index, 1)"
            >
              ↓
            </button>
            <button
              type="button"
              :disabled="busy"
              :aria-label="t('studio.paths.removeFrom', { title: course.title })"
              class="rounded px-2 py-1 text-sm text-danger hover:bg-danger-subtle disabled:opacity-30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
              @click="run((api) => api.removeCourseFromPath(pathId, course.id))"
            >
              {{ t("studio.common.remove") }}
            </button>
          </span>
        </li>
      </ol>

      <p
        v-else
        class="mt-5 rounded-control border border-dashed border-line-strong p-6 text-center text-sm text-ink-muted"
      >
        {{ t("studio.paths.noCourses") }}
      </p>

      <div class="mt-5 flex flex-wrap items-center gap-3">
        <select
          v-model="picked"
          :disabled="busy || available.length === 0"
          :aria-label="t('studio.paths.courseToAdd')"
          class="h-10 min-w-56 flex-1 rounded-control border border-line-strong bg-surface px-3 text-sm text-ink focus:border-accent-strong focus:outline-none focus:ring-2 focus:ring-accent-strong/25"
        >
          <option value="">
            {{ available.length ? t("studio.paths.chooseCourse") : t("studio.paths.allAdded") }}
          </option>
          <option v-for="course in available" :key="course.id" :value="course.id">
            {{ course.title }}{{ course.status === "published" ? "" : ` (${course.status})` }}
          </option>
        </select>
        <DbButton variant="outline" :disabled="busy || !picked" @click="addPicked">
          {{ t("studio.paths.addCourse") }}
        </DbButton>
      </div>
    </section>

    <section v-if="!isNew" class="mt-6 rounded-card border border-line bg-surface p-6">
      <h2 class="font-display text-lg font-bold tracking-tight text-ink">{{ t("studio.common.publishing") }}</h2>
      <p class="mt-1 text-sm text-ink-muted">
        {{ t("studio.paths.publishHint") }}
      </p>

      <div class="mt-4 flex flex-wrap gap-3">
        <DbButton
          :disabled="busy || isPublished || courses.length === 0"
          @click="run((api) => api.publishLearningPath(pathId))"
        >
          {{ t("studio.common.publish") }}
        </DbButton>
        <DbButton
          variant="outline"
          :disabled="busy || !isPublished"
          @click="run((api) => api.unpublishLearningPath(pathId))"
        >
          {{ t("studio.common.unpublish") }}
        </DbButton>
      </div>
    </section>
  </div>
</template>
