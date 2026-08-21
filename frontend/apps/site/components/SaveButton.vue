<script setup lang="ts">
import type { BookmarkKind } from "@databro/types";

/**
 * Save-for-later control.
 *
 * <b>Secondary by construction</b>, like the progress bar and the lesson quiz: the page renders
 * server-side for everyone, and this hydrates in afterwards only for a signed-in learner. A signed
 * out reader sees nothing rather than a control that would ask them to sign in mid-read — the
 * catalogue is for browsing, and an interruption there costs more than the save is worth.
 *
 * State is optimistic. Saving is idempotent server-side and un-saving succeeds even when nothing was
 * saved, so the worst a failed round trip can do is leave the icon out of step until the next load —
 * and it is corrected in the `catch` regardless.
 */
const props = defineProps<{ kind: BookmarkKind; targetId: string }>();

const { t } = useI18n();
const { isSignedIn, tryAuthed } = useLearnerSession();

const saved = ref(false);
const busy = ref(false);
const known = ref(false);

onMounted(async () => {
  if (!isSignedIn.value) return;

  const result = await tryAuthed((api) => api.savedBookmarkIds());
  if (result.ok) {
    saved.value = result.value.includes(props.targetId);
    known.value = true;
  }
});

async function toggle() {
  if (busy.value) return;

  busy.value = true;
  const next = !saved.value;
  saved.value = next; // optimistic

  try {
    // Typed `void` deliberately: the two branches return different shapes and neither is used —
    // what this call needs is whether it succeeded, not what came back.
    const result = await tryAuthed<void>(async (api) => {
      if (next) await api.saveBookmark(props.kind, props.targetId);
      else await api.removeBookmark(props.kind, props.targetId);
    });

    if (!result.ok) saved.value = !next;
  } catch {
    saved.value = !next;
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <!-- Rendered only once the saved state is known, so the icon never flips under the reader a
       moment after the page settles. -->
  <button
    v-if="isSignedIn && known"
    type="button"
    :disabled="busy"
    :aria-pressed="saved"
    :aria-label="saved ? t('bookmarks.remove') : t('bookmarks.save')"
    :title="saved ? t('bookmarks.remove') : t('bookmarks.save')"
    class="inline-flex h-9 items-center gap-1.5 rounded-control border px-3.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong disabled:opacity-50"
    :class="
      saved
        ? 'border-accent-strong bg-accent-subtle text-accent-strong'
        : 'border-line-strong text-ink-muted hover:bg-surface-sunken hover:text-ink'
    "
    @click="toggle"
  >
    <span aria-hidden="true">{{ saved ? "★" : "☆" }}</span>
    <span>{{ saved ? t("bookmarks.saved") : t("bookmarks.save") }}</span>
  </button>
</template>
