<script setup lang="ts">
import { DbButton, DbChip, DbInput } from "@databro/ui";
import { ApiClientError, type ApiClient } from "@databro/api-client";
import type { Course, Difficulty, LessonContentSummary } from "@databro/types";

/**
 * Course builder: the curriculum tree.
 *
 * Every structural change returns the whole course, so this page never reassembles state from a
 * partial response — the server is the authority on what the order now is. That matters most for
 * reordering, which renumbers every sibling (ADR-0013): patching locally would mean reimplementing
 * the normalisation the domain already owns, and the two would drift.
 */
const route = useRoute();
const router = useRouter();
const { withAuth } = useAuth();

const courseId = computed(() => String(route.params.id));
const isNew = computed(() => courseId.value === "new");

const { data: loaded } = await useAsyncData(
  () => `authoring:course:${courseId.value}`,
  async () => (isNew.value ? null : await withAuth((api) => api.getAuthoringCourse(courseId.value))),
  { watch: [courseId] },
);

const course = ref<Course | null>(loaded.value ?? null);

const title = ref(course.value?.title ?? "");
const summary = ref(course.value?.summary ?? "");
const slug = ref(course.value?.slug ?? "");
const difficulty = ref<Difficulty>(course.value?.difficulty ?? "beginner");

const busy = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);

const status = computed(() => course.value?.status ?? "draft");
const isPublished = computed(() => status.value === "published");

// A published course's URL is a promise, so the slug locks once it has been live (CT-2).
const slugLocked = computed(() => Boolean(course.value?.publishedAt));

function describe(error: unknown) {
  if (error instanceof ApiClientError) return error.message;
  return "Something went wrong. Please try again.";
}

/**
 * Runs one mutation and replaces the course wholesale.
 *
 * Every curriculum endpoint returns the full course, so this never patches local state — the server
 * is the authority on what the order now is, and reproducing its renumbering here would be a second
 * implementation of an invariant the domain already owns.
 */
async function run(action: (api: ApiClient) => Promise<Course>) {
  formError.value = null;
  busy.value = true;

  try {
    course.value = await withAuth(action);
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
        api.createCourse({
          title: title.value,
          summary: summary.value,
          slug: slug.value || undefined,
          difficulty: difficulty.value,
        }),
      );

      course.value = created;
      // Move off /new so a reload cannot create a second course.
      await router.replace(`/courses/${created.id}`);
    } catch (error) {
      formError.value = describe(error);
    } finally {
      busy.value = false;
    }
    return;
  }

  await run((api) =>
    api.updateCourse(courseId.value, {
      title: title.value,
      summary: summary.value,
      difficulty: difficulty.value,
    }),
  );
}

// ---- Curriculum ----

const newModuleTitle = ref("");

async function addModule() {
  if (!newModuleTitle.value.trim() || !course.value) return;

  const value = newModuleTitle.value;
  newModuleTitle.value = "";
  await run((api) => api.addCourseModule(course.value!.id, value));
}

/**
 * Moves a module or lesson by one position and sends the resulting order.
 *
 * Buttons rather than drag-and-drop, for the same reason the block editor uses them: buttons are
 * keyboard-operable and screen-reader-announceable for free, where drag needs a parallel keyboard
 * affordance to be usable at all. Drag is an enhancement on top, never a replacement.
 */
function moved(ids: string[], index: number, delta: number): string[] | null {
  const target = index + delta;
  if (target < 0 || target >= ids.length) return null;

  const from = ids[index];
  const to = ids[target];
  if (from === undefined || to === undefined) return null;

  const next = [...ids];
  next[index] = to;
  next[target] = from;
  return next;
}

async function moveModule(index: number, delta: number) {
  if (!course.value) return;

  const next = moved(course.value.modules.map((m) => m.id), index, delta);
  if (!next) return;

  await run((api) => api.reorderCourseModules(course.value!.id, next));
}

async function moveLesson(moduleId: string, index: number, delta: number) {
  const module = course.value?.modules.find((m) => m.id === moduleId);
  if (!module) return;

  const next = moved(module.lessons.map((l) => l.id), index, delta);
  if (!next) return;

  await run((api) => api.reorderCourseLessons(course.value!.id, moduleId, next));
}

async function removeModule(moduleId: string, lessonCount: number) {
  const warning = lessonCount > 0
    ? `Remove this module and its ${lessonCount} lesson(s) from the course? The lesson bodies themselves are not deleted.`
    : "Remove this module?";

  if (!confirm(warning)) return;
  await run((api) => api.removeCourseModule(course.value!.id, moduleId));
}

async function removeLesson(moduleId: string, lessonId: string) {
  // Worth confirming even though it is cheap to undo: the wording is what tells an author the body
  // survives, which is not obvious from a delete button.
  if (!confirm("Remove this lesson from the course? The lesson body is not deleted.")) return;
  await run((api) => api.removeCourseLesson(course.value!.id, moduleId, lessonId));
}

// ---- Attaching a lesson body ----

const picker = ref<{ moduleId: string; bodies: LessonContentSummary[] } | null>(null);
const pickerLoading = ref(false);

async function openPicker(moduleId: string) {
  pickerLoading.value = true;
  formError.value = null;

  try {
    const page = await withAuth((api) => api.listLessonContent({ pageSize: 100 }));
    picker.value = { moduleId, bodies: page.items };
  } catch (error) {
    formError.value = describe(error);
  } finally {
    pickerLoading.value = false;
  }
}

async function attach(contentUnitId: string) {
  const moduleId = picker.value?.moduleId;
  picker.value = null;
  if (!moduleId) return;

  await run((api) => api.addCourseLesson(course.value!.id, moduleId, contentUnitId));
}

/** Bodies already in this course, so the picker can mark them rather than silently 409 on attach. */
const attachedIds = computed(
  () => new Set(course.value?.modules.flatMap((m) => m.lessons.map((l) => l.contentUnitId)) ?? []),
);

// ---- Publishing ----

async function togglePublish() {
  if (!course.value) return;

  await run((api) =>
    isPublished.value ? api.unpublishCourse(course.value!.id) : api.publishCourse(course.value!.id),
  );
}

/** Lessons that will not appear to a learner. The warning the ADR obliges the CMS to show. */
const unpublishedLessons = computed(
  () => course.value?.modules.flatMap((m) => m.lessons).filter((l) => !l.isPublished) ?? [],
);

useHead({ title: computed(() => (isNew.value ? "New course" : title.value || "Edit course")) });
</script>

<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <NuxtLink to="/courses" class="text-sm font-medium text-accent hover:underline">← Courses</NuxtLink>
        <h1 class="mt-2 font-display text-2xl font-bold tracking-tight text-ink">
          {{ isNew ? "New course" : title || "Untitled" }}
        </h1>
        <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
          <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
          <span v-if="course">{{ course.lessonCount }} lesson(s)</span>
          <span v-if="savedAt">Saved {{ savedAt }}</span>
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <DbButton variant="outline" :disabled="busy" @click="saveDetails">
          {{ busy ? "Working…" : isNew ? "Create course" : "Save details" }}
        </DbButton>
        <DbButton
          v-if="!isNew"
          :variant="isPublished ? 'outline' : 'primary'"
          :disabled="busy"
          @click="togglePublish"
        >
          {{ isPublished ? "Unpublish" : "Publish" }}
        </DbButton>
      </div>
    </div>

    <p
      v-if="formError"
      role="alert"
      class="mt-4 rounded-card border border-danger/30 bg-danger-subtle px-4 py-3 text-sm text-danger"
    >
      {{ formError }}
    </p>

    <div class="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <div class="space-y-6">
        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Details</h2>

          <DbInput v-model="title" label="Title" required />
          <DbInput v-model="summary" label="Summary" hint="Shown on the course card." />
          <DbInput
            v-if="isNew"
            v-model="slug"
            label="Slug"
            hint="Leave blank to derive from the title. Immutable once published."
          />
          <p v-else class="text-sm text-ink-muted">
            Slug: <code class="font-mono">{{ course?.slug }}</code>
            <span v-if="slugLocked" class="text-ink-subtle"> — locked; it is a live URL.</span>
          </p>

          <div>
            <label for="difficulty" class="mb-1.5 block text-sm font-medium text-ink-muted">Difficulty</label>
            <select
              id="difficulty"
              v-model="difficulty"
              class="h-10 w-full rounded-md border border-line-strong bg-surface px-3 text-sm"
            >
              <option value="beginner">Beginner</option>
              <option value="intermediate">Intermediate</option>
              <option value="advanced">Advanced</option>
            </select>
          </div>
        </section>

        <!-- Curriculum -->
        <section v-if="course" class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Curriculum</h2>

          <p v-if="course.modules.length === 0" class="text-sm text-ink-muted">
            No modules yet. A module is a named section — “Retrieval”, “Evaluation” — holding an
            ordered run of lessons.
          </p>

          <div
            v-for="(module, moduleIndex) in course.modules"
            :key="module.id"
            class="rounded-md border border-line"
          >
            <div class="flex flex-wrap items-center gap-2 border-b border-line bg-surface-sunken px-3 py-2">
              <span class="font-mono text-xs text-ink-subtle">{{ module.order + 1 }}</span>
              <span class="min-w-0 flex-1 truncate font-medium text-ink">{{ module.title }}</span>

              <button
                type="button"
                class="h-7 w-7 rounded border border-line text-xs text-ink-muted hover:bg-surface disabled:opacity-40"
                :disabled="busy || moduleIndex === 0"
                :aria-label="`Move ${module.title} up`"
                @click="moveModule(moduleIndex, -1)"
              >↑</button>
              <button
                type="button"
                class="h-7 w-7 rounded border border-line text-xs text-ink-muted hover:bg-surface disabled:opacity-40"
                :disabled="busy || moduleIndex === course.modules.length - 1"
                :aria-label="`Move ${module.title} down`"
                @click="moveModule(moduleIndex, 1)"
              >↓</button>
              <button
                type="button"
                class="h-7 w-7 rounded border border-line text-xs text-danger hover:bg-danger-subtle"
                :disabled="busy"
                :aria-label="`Remove ${module.title}`"
                @click="removeModule(module.id, module.lessons.length)"
              >✕</button>
            </div>

            <ul v-if="module.lessons.length" class="divide-y divide-line">
              <li
                v-for="(lesson, lessonIndex) in module.lessons"
                :key="lesson.id"
                class="flex flex-wrap items-center gap-2 px-3 py-2"
              >
                <span class="font-mono text-xs text-ink-subtle">{{ lesson.order + 1 }}</span>
                <NuxtLink
                  :to="`/lessons/${lesson.contentUnitId}`"
                  class="min-w-0 flex-1 truncate text-sm text-accent hover:underline"
                >
                  {{ lesson.title }}
                </NuxtLink>

                <DbChip v-if="!lesson.isPublished" tone="warning">body draft</DbChip>
                <span class="text-xs tabular-nums text-ink-subtle">{{ lesson.estimatedMinutes }} min</span>

                <button
                  type="button"
                  class="h-7 w-7 rounded border border-line text-xs text-ink-muted hover:bg-surface-sunken disabled:opacity-40"
                  :disabled="busy || lessonIndex === 0"
                  :aria-label="`Move ${lesson.title} up`"
                  @click="moveLesson(module.id, lessonIndex, -1)"
                >↑</button>
                <button
                  type="button"
                  class="h-7 w-7 rounded border border-line text-xs text-ink-muted hover:bg-surface-sunken disabled:opacity-40"
                  :disabled="busy || lessonIndex === module.lessons.length - 1"
                  :aria-label="`Move ${lesson.title} down`"
                  @click="moveLesson(module.id, lessonIndex, 1)"
                >↓</button>
                <button
                  type="button"
                  class="h-7 w-7 rounded border border-line text-xs text-danger hover:bg-danger-subtle"
                  :disabled="busy"
                  :aria-label="`Remove ${lesson.title}`"
                  @click="removeLesson(module.id, lesson.id)"
                >✕</button>
              </li>
            </ul>

            <div class="px-3 py-2">
              <DbButton size="sm" variant="soft" :disabled="busy || pickerLoading" @click="openPicker(module.id)">
                Add lesson
              </DbButton>
            </div>
          </div>

          <div class="flex gap-2">
            <input
              v-model="newModuleTitle"
              class="h-9 flex-1 rounded-md border border-line-strong bg-surface px-3 text-sm"
              placeholder="New module title"
              aria-label="New module title"
              @keydown.enter.prevent="addModule"
            />
            <DbButton size="sm" variant="outline" :disabled="busy || !newModuleTitle.trim()" @click="addModule">
              Add module
            </DbButton>
          </div>
        </section>
      </div>

      <aside v-if="course" class="space-y-6 xl:sticky xl:top-6 xl:self-start">
        <!-- The warning ADR-0013 obliges: publishing early is an affordance only if the gaps are
             visible where an author can act on them. -->
        <section
          v-if="unpublishedLessons.length"
          class="rounded-card border border-warning/30 bg-warning-subtle p-4 text-sm"
        >
          <h2 class="font-semibold text-ink">
            {{ unpublishedLessons.length }} lesson(s) will not be visible
          </h2>
          <p class="mt-1 text-ink-muted">
            Their bodies are still drafts. The course can be published without them — a learner
            simply will not see them until each body is published.
          </p>
          <ul class="mt-2 space-y-1">
            <li v-for="lesson in unpublishedLessons" :key="lesson.id">
              <NuxtLink :to="`/lessons/${lesson.contentUnitId}`" class="text-accent hover:underline">
                {{ lesson.title }}
              </NuxtLink>
            </li>
          </ul>
        </section>

        <section class="rounded-card border border-line bg-surface p-5 text-sm">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">At a glance</h2>
          <dl class="mt-3 grid grid-cols-2 gap-y-2">
            <dt class="text-ink-muted">Modules</dt>
            <dd class="text-right tabular-nums text-ink">{{ course.modules.length }}</dd>
            <dt class="text-ink-muted">Lessons</dt>
            <dd class="text-right tabular-nums text-ink">{{ course.lessonCount }}</dd>
            <dt class="text-ink-muted">Duration</dt>
            <dd class="text-right tabular-nums text-ink">{{ course.estimatedMinutes }} min</dd>
          </dl>
          <p class="mt-3 text-xs text-ink-subtle">
            Duration is summed from the lessons, so it cannot drift from the curriculum.
          </p>
        </section>
      </aside>
    </div>

    <!-- Lesson picker -->
    <div v-if="picker" class="fixed inset-0 z-50 flex items-start justify-center bg-ink/40 p-4 pt-20">
      <div class="max-h-[70vh] w-full max-w-2xl overflow-y-auto rounded-card border border-line bg-surface p-5">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="font-display text-base font-semibold text-ink">Attach a lesson body</h2>
          <button type="button" class="text-sm text-ink-muted hover:text-ink" @click="picker = null">
            Close
          </button>
        </div>

        <p v-if="picker.bodies.length === 0" class="text-sm text-ink-muted">
          No lesson bodies yet. <NuxtLink to="/lessons/new" class="text-accent hover:underline">Write one</NuxtLink>
          first, then attach it here.
        </p>

        <ul v-else class="divide-y divide-line">
          <li v-for="body in picker.bodies" :key="body.id" class="flex items-center gap-3 py-2">
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium text-ink">{{ body.title }}</p>
              <p class="truncate text-xs text-ink-subtle">{{ body.slug }}</p>
            </div>
            <DbChip :tone="body.status === 'published' ? 'success' : 'neutral'">{{ body.status }}</DbChip>
            <DbButton
              size="sm"
              variant="soft"
              :disabled="attachedIds.has(body.id)"
              @click="attach(body.id)"
            >
              {{ attachedIds.has(body.id) ? "Already in course" : "Attach" }}
            </DbButton>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
