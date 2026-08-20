<script setup lang="ts">
import { DbButton } from "@databro/ui";
import type { MediaAsset } from "@databro/types";

const { t } = useI18n();

/**
 * Image chooser for the block editor (ADR-0011).
 *
 * Two ways in, because they are genuinely different tasks: upload a new file, or reuse one already
 * in the library. A picker that only uploads guarantees the same image gets stored four times.
 */
const props = defineProps<{ modelValue: string }>();
const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const { withAuth } = useAuth();
// Everything this picker resolves is remembered so the live preview can render it immediately,
// rather than waiting for a save-and-reload to get it into the article's own media map.
const { remember } = useMediaCache();

const open = ref(false);
const library = ref<MediaAsset[]>([]);
const selected = ref<MediaAsset | null>(null);
const loading = ref(false);
const uploading = ref(false);
const error = ref("");
const fileInput = ref<HTMLInputElement | null>(null);

/**
 * Resolves the currently-referenced id to something showable.
 *
 * A block edited before the asset existed — or one whose asset was deleted — keeps its id and shows
 * nothing, which is the honest state rather than a broken thumbnail.
 */
async function loadSelected() {
  if (!props.modelValue) {
    selected.value = null;
    return;
  }

  if (selected.value?.id === props.modelValue) return;

  try {
    const asset = await withAuth((api) => api.getMediaAsset(props.modelValue));
    selected.value = asset;
    remember(asset);
  } catch {
    selected.value = null;
  }
}

watch(() => props.modelValue, loadSelected, { immediate: true });

async function openLibrary() {
  open.value = true;
  loading.value = true;
  error.value = "";

  try {
    const page = await withAuth((api) => api.listMedia({ pageSize: 40 }));
    library.value = page.items;
  } catch (e) {
    error.value = e instanceof Error ? e.message : t("studio.media.loadFailed");
  } finally {
    loading.value = false;
  }
}

function choose(asset: MediaAsset) {
  selected.value = asset;
  remember(asset);
  emit("update:modelValue", asset.id);
  open.value = false;
}

function clear() {
  selected.value = null;
  emit("update:modelValue", "");
}

async function upload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;

  uploading.value = true;
  error.value = "";

  try {
    const asset = await withAuth((api) => api.uploadMedia(file));
    choose(asset);

    // Variants come from a background job, so the asset arrives Pending. One delayed re-read is
    // enough to pick up the finished state in practice; if it is still pending the badge says so
    // rather than the UI pretending otherwise.
    if (asset.processingStatus === "pending") {
      setTimeout(() => void loadSelected(), 2000);
    }
  } catch (e) {
    error.value = e instanceof Error ? e.message : t("studio.media.uploadFailed");
  } finally {
    uploading.value = false;
    // Reset so re-selecting the same file fires `change` again.
    input.value = "";
  }
}
</script>

<template>
  <div class="space-y-3">
    <div class="flex flex-wrap items-center gap-2">
      <input
        ref="fileInput"
        type="file"
        accept="image/jpeg,image/png,image/webp,image/gif"
        class="hidden"
        @change="upload"
      />
      <DbButton type="button" size="sm" :disabled="uploading" @click="fileInput?.click()">
        {{ uploading ? "Uploading…" : "Upload image" }}
      </DbButton>
      <DbButton type="button" size="sm" variant="secondary" @click="openLibrary">
        {{ t("studio.media.chooseFromLibrary") }}
      </DbButton>
      <DbButton v-if="modelValue" type="button" size="sm" variant="ghost" @click="clear">
        {{ t("studio.common.remove") }}
      </DbButton>
    </div>

    <p v-if="error" class="text-sm text-danger">{{ error }}</p>

    <!-- Current selection -->
    <div v-if="selected" class="flex items-start gap-3 rounded-control border border-line p-3">
      <img
        :src="selected.variants[0]?.url ?? selected.url"
        :alt="selected.altText"
        class="h-20 w-20 rounded object-cover"
      />
      <div class="min-w-0 text-sm">
        <p class="truncate font-medium text-ink">{{ selected.fileName }}</p>
        <p class="text-ink-muted">{{ selected.width }}×{{ selected.height }}</p>
        <p v-if="selected.processingStatus === 'pending'" class="text-ink-subtle">
          {{ t("studio.media.generating") }}
        </p>
        <p v-else-if="selected.processingStatus === 'failed'" class="text-danger">
          {{ t("studio.media.variantsFailed") }}
        </p>
        <p v-else class="text-ink-subtle">{{ selected.variants.length }} responsive sizes</p>
      </div>
    </div>

    <p v-else-if="modelValue" class="text-sm text-ink-subtle">
      {{ t("studio.media.referenced") }} <code class="font-mono">{{ modelValue }}</code>
      {{ t("studio.media.couldNotLoad") }}
    </p>

    <!-- Library -->
    <div v-if="open" class="rounded-control border border-line p-3">
      <div class="mb-3 flex items-center justify-between">
        <h3 class="text-sm font-semibold text-ink">{{ t("studio.media.library") }}</h3>
        <button type="button" class="text-sm text-ink-muted hover:text-ink" @click="open = false">
          {{ t("studio.common.close") }}
        </button>
      </div>

      <p v-if="loading" class="text-sm text-ink-muted">{{ t("studio.common.loading") }}</p>
      <p v-else-if="library.length === 0" class="text-sm text-ink-muted">
        {{ t("studio.media.nothingUploaded") }}
      </p>
      <div v-else class="grid max-h-72 grid-cols-3 gap-2 overflow-y-auto sm:grid-cols-5">
        <button
          v-for="asset in library"
          :key="asset.id"
          type="button"
          class="group rounded border border-line p-1 text-left hover:border-accent-strong"
          @click="choose(asset)"
        >
          <img
            :src="asset.variants[0]?.url ?? asset.url"
            :alt="asset.altText"
            class="h-20 w-full rounded object-cover"
          />
          <span class="mt-1 block truncate text-xs text-ink-muted">{{ asset.fileName }}</span>
        </button>
      </div>
    </div>
  </div>
</template>
