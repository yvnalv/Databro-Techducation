<script setup lang="ts">
// The CMS shell, not the learner one (ADR-0015). Every page under /studio opts in explicitly:
// Nuxt's default is `default.vue`, and a studio page silently rendering the learner chrome would be
// a confusing failure rather than a loud one.
definePageMeta({ layout: "studio" });

import { ContentRenderer, DbButton, DbChip, DbInput, mediaResolverFor } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";
import type { ArticleVersionSummary, ContentDocument, LessonContent } from "@databro/types";

/**
 * Lesson body editor.
 *
 * Deliberately the article editor minus everything a lesson does not have: no taxonomy, no SEO, no
 * scheduling. It reuses the same `BlockEditor` and the same preview renderer, which is ADR-0007
 * paying out at the UI layer too — one editor, not two that drift.
 *
 * Where the lesson *sits* is not edited here. Position, objectives and difficulty belong to the
 * curriculum, so they live in the course builder.
 */
const route = useRoute();
const router = useRouter();
const { withAuth } = useAuth();

const lessonId = computed(() => String(route.params.id));
const isNew = computed(() => lessonId.value === "new");

const { data: loaded } = await useAsyncData(
  () => `authoring:lesson:${lessonId.value}`,
  async () => (isNew.value ? null : await withAuth((api) => api.getLessonContent(lessonId.value))),
  { watch: [lessonId] },
);

const lesson = ref<LessonContent | null>(loaded.value ?? null);

const title = ref(lesson.value?.title ?? "");
const summary = ref(lesson.value?.summary ?? "");
const slug = ref(lesson.value?.slug ?? "");
const content = ref<ContentDocument>(lesson.value?.content ?? { version: 1, blocks: [] });

const busy = ref(false);
const formError = ref<string | null>(null);
const savedAt = ref<string | null>(null);

const status = computed(() => lesson.value?.status ?? "draft");
const isPublished = computed(() => status.value === "published");
const slugLocked = computed(() => Boolean(lesson.value?.publishedAt));

const { merged } = useMediaCache();
const resolveMedia = computed(() => mediaResolverFor(merged(undefined)));

function describe(error: unknown) {
  if (error instanceof ApiClientError) return error.message;
  return "Something went wrong. Please try again.";
}

async function save() {
  formError.value = null;
  busy.value = true;

  try {
    if (isNew.value) {
      const created = await withAuth((api) =>
        api.createLessonContent({
          title: title.value,
          summary: summary.value,
          content: content.value,
          slug: slug.value || undefined,
        }),
      );

      lesson.value = created;
      // Move off /new so a reload cannot create a second body.
      await router.replace(`/studio/lessons/${created.id}`);
      return;
    }

    lesson.value = await withAuth((api) =>
      api.updateLessonContent(lessonId.value, {
        title: title.value,
        summary: summary.value,
        content: content.value,
      }),
    );

    savedAt.value = new Date().toLocaleTimeString();
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

async function togglePublish() {
  formError.value = null;
  busy.value = true;

  try {
    // Publish snapshots the *saved* draft, so an unsaved edit would silently not go live.
    await save();
    if (formError.value) return;

    const id = lesson.value?.id;
    if (!id) return;

    lesson.value = isPublished.value
      ? await withAuth((api) => api.unpublishLessonContent(id))
      : await withAuth((api) => api.publishLessonContent(id));
  } catch (error) {
    formError.value = describe(error);
  } finally {
    busy.value = false;
  }
}

// ---- Version history, free from the shared engine ----

const versions = ref<ArticleVersionSummary[]>([]);
const versionsOpen = ref(false);
const restoring = ref<number | null>(null);

async function toggleVersions() {
  versionsOpen.value = !versionsOpen.value;
  if (!versionsOpen.value || versions.value.length || !lesson.value) return;

  try {
    versions.value = await withAuth((api) => api.listLessonContentVersions(lesson.value!.id));
  } catch (error) {
    formError.value = describe(error);
  }
}

async function restore(version: number) {
  const id = lesson.value?.id;
  if (!id) return;

  if (!confirm(`Restore version ${version}? This replaces the current draft. Nothing published changes.`))
    return;

  restoring.value = version;

  try {
    const restored = await withAuth((api) => api.restoreLessonContentVersion(id, version));
    lesson.value = restored;
    title.value = restored.title;
    summary.value = restored.summary;
    content.value = restored.content;
  } catch (error) {
    formError.value = describe(error);
  } finally {
    restoring.value = null;
  }
}

watch(() => lesson.value?.currentVersion, () => {
  if (versionsOpen.value) versions.value = [];
});

useHead({ title: computed(() => (isNew.value ? "New lesson" : title.value || "Edit lesson")) });
</script>

<template>
  <div>
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div class="min-w-0">
        <NuxtLink to="/studio/lessons" class="text-sm font-medium text-accent hover:underline">← Lessons</NuxtLink>
        <h1 class="mt-2 font-display text-2xl font-bold tracking-tight text-ink">
          {{ isNew ? "New lesson" : title || "Untitled" }}
        </h1>
        <p class="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink-muted">
          <DbChip :tone="isPublished ? 'success' : 'neutral'">{{ status }}</DbChip>
          <span v-if="savedAt">Saved {{ savedAt }}</span>
        </p>
      </div>

      <div class="flex flex-wrap gap-2">
        <DbButton variant="outline" :disabled="busy" @click="save">
          {{ busy ? "Working…" : isNew ? "Create lesson" : "Save draft" }}
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

    <p
      v-if="isPublished"
      class="mt-4 rounded-card border border-line bg-surface-sunken px-4 py-3 text-sm text-ink-muted"
    >
      Unpublishing removes this lesson from every course that uses it, without warning the courses.
      Content has no way to ask which curricula depend on a body — that is the module boundary, not
      an oversight.
    </p>

    <div class="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_26rem]">
      <div class="space-y-6">
        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Details</h2>

          <DbInput v-model="title" label="Title" required />
          <DbInput v-model="summary" label="Summary" hint="Shown in the course outline." />
          <DbInput
            v-if="isNew"
            v-model="slug"
            label="Slug"
            hint="Leave blank to derive from the title. Must be unique across articles and lessons alike."
          />
          <p v-else class="text-sm text-ink-muted">
            Slug: <code class="font-mono">{{ lesson?.slug }}</code>
            <span v-if="slugLocked" class="text-ink-subtle"> — locked once published.</span>
          </p>
        </section>

        <section class="space-y-4 rounded-card border border-line bg-surface p-5">
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Content</h2>
          <BlockEditor v-model="content" />
        </section>

        <section v-if="!isNew" class="space-y-4 rounded-card border border-line bg-surface p-5">
          <div class="flex items-center justify-between">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">
              Version history
            </h2>
            <button
              type="button"
              class="text-sm font-medium text-accent hover:underline"
              :aria-expanded="versionsOpen"
              @click="toggleVersions"
            >
              {{ versionsOpen ? "Hide" : "Show" }}
            </button>
          </div>

          <template v-if="versionsOpen">
            <p v-if="versions.length === 0" class="text-sm text-ink-muted">
              No versions yet — history starts at the first publish.
            </p>
            <ul v-else class="divide-y divide-line">
              <li v-for="v in versions" :key="v.version" class="flex items-start gap-3 py-3">
                <div class="min-w-0 flex-1">
                  <p class="flex items-center gap-2 text-sm font-medium text-ink">
                    v{{ v.version }}
                    <DbChip v-if="v.isCurrent" tone="success">live</DbChip>
                  </p>
                  <p class="truncate text-sm text-ink-muted">{{ v.title }}</p>
                  <p class="text-xs text-ink-subtle">{{ new Date(v.createdAt).toLocaleString() }}</p>
                </div>
                <DbButton
                  variant="ghost"
                  size="sm"
                  :disabled="restoring !== null"
                  @click="restore(v.version)"
                >
                  {{ restoring === v.version ? "Restoring…" : "Restore" }}
                </DbButton>
              </li>
            </ul>
          </template>
        </section>
      </div>

      <aside class="xl:sticky xl:top-6 xl:self-start">
        <div class="rounded-card border border-line bg-surface">
          <div class="border-b border-line px-4 py-2">
            <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-ink">Preview</h2>
          </div>
          <div class="max-h-[70vh] overflow-y-auto p-5">
            <h3 class="font-display text-xl font-bold tracking-tight text-ink">{{ title || "Untitled" }}</h3>
            <p v-if="summary" class="mt-2 text-sm text-ink-muted">{{ summary }}</p>
            <hr class="my-4 border-line" />
            <!-- The same renderer the public site uses, so preview and production cannot drift. -->
            <ContentRenderer :document="content" :resolve-media="resolveMedia" show-unknown-blocks />
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>
