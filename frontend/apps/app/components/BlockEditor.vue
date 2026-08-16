<script setup lang="ts">
import { DbButton, SUPPORTED_BLOCK_TYPES } from "@databro/ui";
import type { ContentBlock, ContentDocument, RichText } from "@databro/types";

/**
 * Block list editor (docs/CONTENT_MODEL.md).
 *
 * Reorder is buttons, not drag-and-drop: buttons are keyboard-operable and screen-reader-announceable
 * for free, where drag needs a parallel keyboard affordance to be usable at all. Drag is a later
 * enhancement on top, never a replacement.
 */
const props = defineProps<{ modelValue: ContentDocument }>();
const emit = defineEmits<{ "update:modelValue": [value: ContentDocument] }>();

const blocks = computed(() => props.modelValue?.blocks ?? []);

function commit(next: ContentBlock[]) {
  emit("update:modelValue", { version: props.modelValue?.version ?? 1, blocks: next });
}

/** Block ids must be stable and unique — the renderer keys on them (CONTENT_MODEL invariants). */
const newId = () => `b${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;

const EMPTY_DATA: Record<string, unknown> = {
  heading: { level: 2, text: "" },
  paragraph: { content: [] },
  code: { language: "python", code: "" },
  callout: { variant: "tip", content: [] },
  image: { mediaId: "", alt: "" },
  quote: { content: [], attribution: "" },
  list: { ordered: false, items: [{ content: [] }] },
  divider: {},
  embed: { provider: "", url: "" },
  table: { headers: [[]], rows: [[[]]] },
  math: { latex: "" },
};

function addBlock(type: string) {
  commit([
    ...blocks.value,
    { id: newId(), type, data: structuredClone(EMPTY_DATA[type] ?? {}) } as ContentBlock,
  ]);
}

function updateData(index: number, data: unknown) {
  const next = [...blocks.value];
  next[index] = { ...next[index], data } as ContentBlock;
  commit(next);
}

function move(index: number, delta: number) {
  const target = index + delta;
  if (target < 0 || target >= blocks.value.length) return;

  const next = [...blocks.value];
  const moved = next[index]!;
  next[index] = next[target]!;
  next[target] = moved;
  commit(next);
}

function remove(index: number) {
  commit(blocks.value.filter((_, i) => i !== index));
}

// A list item's inline content, normalized to the object form even when the stored value is a
// legacy plain string (the ADR-0009 compatibility shim).
function itemContent(item: unknown): RichText {
  if (typeof item === "string") return [{ type: "text", text: item }];
  return (item as { content?: RichText } | null)?.content ?? [];
}
</script>

<template>
  <div class="space-y-3">
    <p v-if="!blocks.length" class="rounded-card border border-dashed border-line-strong p-6 text-center text-sm text-ink-muted">
      No blocks yet. Add one below.
    </p>

    <div
      v-for="(block, index) in blocks"
      :key="block.id"
      class="rounded-card border border-line bg-surface"
    >
      <div class="flex items-center justify-between gap-2 border-b border-line px-3 py-2">
        <span class="font-mono text-xs uppercase tracking-wide text-ink-subtle">{{ block.type }}</span>

        <span class="flex items-center gap-1">
          <button
            type="button"
            class="h-7 w-7 rounded border border-line text-xs text-ink-muted transition-colors hover:bg-surface-sunken disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            :disabled="index === 0"
            :aria-label="`Move ${block.type} block up`"
            @click="move(index, -1)"
          >
            ↑
          </button>
          <button
            type="button"
            class="h-7 w-7 rounded border border-line text-xs text-ink-muted transition-colors hover:bg-surface-sunken disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            :disabled="index === blocks.length - 1"
            :aria-label="`Move ${block.type} block down`"
            @click="move(index, 1)"
          >
            ↓
          </button>
          <button
            type="button"
            class="h-7 rounded border border-line px-2 text-xs text-danger transition-colors hover:bg-danger-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-danger"
            :aria-label="`Delete ${block.type} block`"
            @click="remove(index)"
          >
            Delete
          </button>
        </span>
      </div>

      <div class="space-y-2 p-3">
        <!-- heading -->
        <template v-if="block.type === 'heading'">
          <div class="flex gap-2">
            <select
              :value="(block.data as any).level ?? 2"
              class="h-9 rounded-md border border-line-strong bg-surface px-2 text-sm"
              aria-label="Heading level"
              @change="updateData(index, { ...(block.data as any), level: Number(($event.target as HTMLSelectElement).value) })"
            >
              <option :value="2">H2</option>
              <option :value="3">H3</option>
              <option :value="4">H4</option>
            </select>
            <input
              :value="(block.data as any).text"
              class="h-9 flex-1 rounded-md border border-line-strong bg-surface px-3 text-sm"
              placeholder="Heading text"
              aria-label="Heading text"
              @input="updateData(index, { ...(block.data as any), text: ($event.target as HTMLInputElement).value })"
            />
          </div>
        </template>

        <!-- paragraph / quote / callout: inline rich text -->
        <template v-else-if="['paragraph', 'quote', 'callout'].includes(block.type)">
          <select
            v-if="block.type === 'callout'"
            :value="(block.data as any).variant ?? 'tip'"
            class="mb-2 h-9 rounded-md border border-line-strong bg-surface px-2 text-sm"
            aria-label="Callout variant"
            @change="updateData(index, { ...(block.data as any), variant: ($event.target as HTMLSelectElement).value })"
          >
            <option value="info">Info</option>
            <option value="tip">Tip</option>
            <option value="warning">Warning</option>
            <option value="danger">Danger</option>
          </select>

          <RichTextEditor
            :model-value="(block.data as any).content ?? []"
            @update:model-value="updateData(index, { ...(block.data as any), content: $event })"
          />

          <input
            v-if="block.type === 'quote'"
            :value="(block.data as any).attribution ?? ''"
            class="h-9 w-full rounded-md border border-line-strong bg-surface px-3 text-sm"
            placeholder="Attribution (optional)"
            aria-label="Quote attribution"
            @input="updateData(index, { ...(block.data as any), attribution: ($event.target as HTMLInputElement).value })"
          />
        </template>

        <!-- code -->
        <template v-else-if="block.type === 'code'">
          <div class="flex gap-2">
            <input
              :value="(block.data as any).language"
              class="h-9 w-32 rounded-md border border-line-strong bg-surface px-3 font-mono text-sm"
              placeholder="language"
              aria-label="Code language"
              @input="updateData(index, { ...(block.data as any), language: ($event.target as HTMLInputElement).value })"
            />
            <input
              :value="(block.data as any).filename ?? ''"
              class="h-9 flex-1 rounded-md border border-line-strong bg-surface px-3 font-mono text-sm"
              placeholder="filename (optional)"
              aria-label="Code filename"
              @input="updateData(index, { ...(block.data as any), filename: ($event.target as HTMLInputElement).value })"
            />
          </div>
          <textarea
            :value="(block.data as any).code"
            rows="6"
            class="w-full rounded-md border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            placeholder="Code"
            aria-label="Code"
            @input="updateData(index, { ...(block.data as any), code: ($event.target as HTMLTextAreaElement).value })"
          ></textarea>
          <textarea
            :value="(block.data as any).output ?? ''"
            rows="2"
            class="w-full rounded-md border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            placeholder="Output (optional)"
            aria-label="Code output"
            @input="updateData(index, { ...(block.data as any), output: ($event.target as HTMLTextAreaElement).value })"
          ></textarea>
        </template>

        <!-- math -->
        <template v-else-if="block.type === 'math'">
          <textarea
            :value="(block.data as any).latex"
            rows="3"
            class="w-full rounded-md border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            placeholder="LaTeX, e.g. E = mc^2"
            aria-label="LaTeX"
            @input="updateData(index, { latex: ($event.target as HTMLTextAreaElement).value })"
          ></textarea>
        </template>

        <!-- image -->
        <template v-else-if="block.type === 'image'">
          <MediaPicker
            :model-value="(block.data as any).mediaId"
            @update:model-value="updateData(index, { ...(block.data as any), mediaId: $event })"
          />
          <input
            :value="(block.data as any).alt"
            class="h-9 w-full rounded-md border border-line-strong bg-surface px-3 text-sm"
            placeholder="Alt text (required — describes the image)"
            aria-label="Alt text"
            @input="updateData(index, { ...(block.data as any), alt: ($event.target as HTMLInputElement).value })"
          />
          <!-- Per-block, not per-asset, on purpose: the same image can carry different meaning in
               different articles, and the renderer prefers this over the asset's stored text. -->
          <p class="text-xs text-ink-subtle">
            Alt text describes the image in <em>this</em> article. Leave empty only if the image is
            purely decorative.
          </p>
        </template>

        <!-- embed -->
        <template v-else-if="block.type === 'embed'">
          <input
            :value="(block.data as any).url"
            class="h-9 w-full rounded-md border border-line-strong bg-surface px-3 text-sm"
            placeholder="https://… (YouTube, Vimeo or CodePen)"
            aria-label="Embed URL"
            @input="updateData(index, { ...(block.data as any), url: ($event.target as HTMLInputElement).value })"
          />
          <p class="text-xs text-ink-subtle">
            Only allowlisted providers are framed; anything else renders as a plain link.
          </p>
        </template>

        <!-- list -->
        <template v-else-if="block.type === 'list'">
          <label class="flex items-center gap-2 text-sm text-ink-muted">
            <input
              type="checkbox"
              :checked="(block.data as any).ordered"
              @change="updateData(index, { ...(block.data as any), ordered: ($event.target as HTMLInputElement).checked })"
            />
            Numbered
          </label>

          <div v-for="(item, i) in (block.data as any).items ?? []" :key="i" class="flex gap-2">
            <RichTextEditor
              class="flex-1"
              :model-value="itemContent(item)"
              @update:model-value="
                updateData(index, {
                  ...(block.data as any),
                  items: (block.data as any).items.map((it: any, j: number) =>
                    j === i ? { ...(typeof it === 'string' ? {} : it), content: $event } : it,
                  ),
                })
              "
            />
            <button
              type="button"
              class="h-9 shrink-0 self-start rounded border border-line px-2 text-xs text-danger hover:bg-danger-subtle"
              :aria-label="`Remove item ${i + 1}`"
              @click="updateData(index, { ...(block.data as any), items: (block.data as any).items.filter((_: any, j: number) => j !== i) })"
            >
              ✕
            </button>
          </div>

          <DbButton
            size="sm"
            variant="soft"
            @click="updateData(index, { ...(block.data as any), items: [...((block.data as any).items ?? []), { content: [] }] })"
          >
            Add item
          </DbButton>
        </template>

        <!-- divider has no data; table is edit-by-JSON until it earns a grid editor -->
        <template v-else-if="block.type === 'divider'">
          <p class="text-sm text-ink-subtle">No settings.</p>
        </template>

        <template v-else>
          <p class="text-sm text-ink-subtle">
            No form for <code class="font-mono">{{ block.type }}</code> yet — it still renders.
          </p>
        </template>
      </div>
    </div>

    <div class="flex flex-wrap gap-2 rounded-card border border-line bg-surface-sunken p-3">
      <span class="w-full text-xs font-medium uppercase tracking-wide text-ink-subtle">Add block</span>
      <DbButton
        v-for="type in SUPPORTED_BLOCK_TYPES"
        :key="type"
        size="sm"
        variant="outline"
        @click="addBlock(type)"
      >
        {{ type }}
      </DbButton>
    </div>
  </div>
</template>
