<script setup lang="ts">
import { DbButton, SUPPORTED_BLOCK_TYPES } from "@databro/ui";
import type { ContentBlock, ContentDocument, RichText } from "@databro/types";

const { t } = useI18n();

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
  // Two columns and one body row: enough of a grid to see what it is, without pre-filling cells an
  // author then has to delete.
  table: { headers: [[], []], rows: [[[], []]] },
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

// ---- Table editing ----
//
// Every mutation below returns a whole new `data` object and keeps the grid **rectangular**: the
// renderer maps headers to `<th scope="col">` and each row's cells positionally, so a row that is
// one cell short silently shifts every value after it into the wrong column. The editor is the only
// place that can guarantee that, since the stored shape is free-form JSONB.

/** A cell is inline content (ADR-0009) or a bare string from before it. */
function tableCell(value: unknown): RichText {
  if (typeof value === "string") return value ? [{ type: "text", text: value }] : [];
  return Array.isArray(value) ? (value as RichText) : [];
}

function tableHeaders(data: unknown): RichText[] {
  const headers = (data as { headers?: unknown[] } | null)?.headers ?? [];
  return headers.length ? headers.map(tableCell) : [[]];
}

function tableRows(data: unknown): RichText[][] {
  const rows = (data as { rows?: unknown[][] } | null)?.rows ?? [];
  const columns = tableColumnCount(data);

  // Padded on read as well as on write: a document authored elsewhere, or one edited before this
  // form existed, can be ragged, and the editor should repair it rather than render a broken grid.
  return rows.map((row) =>
    Array.from({ length: columns }, (_, c) => tableCell((row ?? [])[c])),
  );
}

function tableColumnCount(data: unknown): number {
  const d = data as { headers?: unknown[]; rows?: unknown[][] } | null;
  const widest = Math.max(
    d?.headers?.length ?? 0,
    ...(d?.rows ?? []).map((row) => row?.length ?? 0),
    1,
  );
  return widest;
}

function tableData(headers: RichText[], rows: RichText[][]) {
  return { headers, rows };
}

function setHeader(data: unknown, column: number, content: RichText) {
  const headers = [...tableHeaders(data)];
  headers[column] = content;
  return tableData(headers, tableRows(data));
}

function setCell(data: unknown, row: number, column: number, content: RichText) {
  const rows = tableRows(data).map((r) => [...r]);
  if (rows[row]) rows[row][column] = content;
  return tableData(tableHeaders(data), rows);
}

function addColumn(data: unknown) {
  return tableData(
    [...tableHeaders(data), []],
    tableRows(data).map((row) => [...row, []]),
  );
}

function removeColumn(data: unknown) {
  const columns = tableColumnCount(data);
  if (columns <= 1) return data; // A table with no columns is not a table.

  return tableData(
    tableHeaders(data).slice(0, columns - 1),
    tableRows(data).map((row) => row.slice(0, columns - 1)),
  );
}

function addRow(data: unknown) {
  const columns = tableColumnCount(data);
  return tableData(tableHeaders(data), [
    ...tableRows(data),
    Array.from({ length: columns }, () => [] as RichText),
  ]);
}

function removeRow(data: unknown, row: number) {
  return tableData(
    tableHeaders(data),
    tableRows(data).filter((_, r) => r !== row),
  );
}
</script>

<template>
  <div class="space-y-3">
    <p v-if="!blocks.length" class="rounded-card border border-dashed border-line-strong p-6 text-center text-sm text-ink-muted">
      {{ t("studio.blocks.noBlocks") }}
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
            class="h-7 w-7 rounded border border-line text-xs text-ink-muted transition-colors hover:bg-surface-sunken disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
            :disabled="index === 0"
            :aria-label="t('studio.blocks.moveUp', { type: block.type })"
            @click="move(index, -1)"
          >
            ↑
          </button>
          <button
            type="button"
            class="h-7 w-7 rounded border border-line text-xs text-ink-muted transition-colors hover:bg-surface-sunken disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
            :disabled="index === blocks.length - 1"
            :aria-label="t('studio.blocks.moveDown', { type: block.type })"
            @click="move(index, 1)"
          >
            ↓
          </button>
          <button
            type="button"
            class="h-7 rounded border border-line px-2 text-xs text-danger transition-colors hover:bg-danger-subtle focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-danger"
            :aria-label="t('studio.blocks.deleteBlock', { type: block.type })"
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
              class="h-9 rounded-control border border-line-strong bg-surface px-2 text-sm"
              :aria-label="t('studio.blocks.headingLevel')"
              @change="updateData(index, { ...(block.data as any), level: Number(($event.target as HTMLSelectElement).value) })"
            >
              <option :value="2">H2</option>
              <option :value="3">H3</option>
              <option :value="4">H4</option>
            </select>
            <input
              :value="(block.data as any).text"
              class="h-9 flex-1 rounded-control border border-line-strong bg-surface px-3 text-sm"
              placeholder="Heading text"
              :aria-label="t('studio.blocks.headingText')"
              @input="updateData(index, { ...(block.data as any), text: ($event.target as HTMLInputElement).value })"
            />
          </div>
        </template>

        <!-- paragraph / quote / callout: inline rich text -->
        <template v-else-if="['paragraph', 'quote', 'callout'].includes(block.type)">
          <select
            v-if="block.type === 'callout'"
            :value="(block.data as any).variant ?? 'tip'"
            class="mb-2 h-9 rounded-control border border-line-strong bg-surface px-2 text-sm"
            :aria-label="t('studio.blocks.calloutVariant')"
            @change="updateData(index, { ...(block.data as any), variant: ($event.target as HTMLSelectElement).value })"
          >
            <option value="info">{{ t("studio.blocks.info") }}</option>
            <option value="tip">{{ t("studio.blocks.tip") }}</option>
            <option value="warning">{{ t("studio.blocks.warning") }}</option>
            <option value="danger">{{ t("studio.blocks.danger") }}</option>
          </select>

          <RichTextEditor
            :model-value="(block.data as any).content ?? []"
            @update:model-value="updateData(index, { ...(block.data as any), content: $event })"
          />

          <input
            v-if="block.type === 'quote'"
            :value="(block.data as any).attribution ?? ''"
            class="h-9 w-full rounded-control border border-line-strong bg-surface px-3 text-sm"
            :placeholder="t('studio.blocks.attributionOptional')"
            :aria-label="t('studio.blocks.quoteAttribution')"
            @input="updateData(index, { ...(block.data as any), attribution: ($event.target as HTMLInputElement).value })"
          />
        </template>

        <!-- code -->
        <template v-else-if="block.type === 'code'">
          <div class="flex gap-2">
            <input
              :value="(block.data as any).language"
              class="h-9 w-32 rounded-control border border-line-strong bg-surface px-3 font-mono text-sm"
              :placeholder="t('studio.blocks.languagePlaceholder')"
              :aria-label="t('studio.blocks.codeLanguage')"
              @input="updateData(index, { ...(block.data as any), language: ($event.target as HTMLInputElement).value })"
            />
            <input
              :value="(block.data as any).filename ?? ''"
              class="h-9 flex-1 rounded-control border border-line-strong bg-surface px-3 font-mono text-sm"
              :placeholder="t('studio.blocks.filenamePlaceholder')"
              :aria-label="t('studio.blocks.codeFilename')"
              @input="updateData(index, { ...(block.data as any), filename: ($event.target as HTMLInputElement).value })"
            />
          </div>
          <textarea
            :value="(block.data as any).code"
            rows="6"
            class="w-full rounded-control border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            placeholder="Code"
            :aria-label="t('studio.blocks.code')"
            @input="updateData(index, { ...(block.data as any), code: ($event.target as HTMLTextAreaElement).value })"
          ></textarea>
          <textarea
            :value="(block.data as any).output ?? ''"
            rows="2"
            class="w-full rounded-control border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            :placeholder="t('studio.blocks.outputOptional')"
            :aria-label="t('studio.blocks.codeOutput')"
            @input="updateData(index, { ...(block.data as any), output: ($event.target as HTMLTextAreaElement).value })"
          ></textarea>
        </template>

        <!-- math -->
        <template v-else-if="block.type === 'math'">
          <textarea
            :value="(block.data as any).latex"
            rows="3"
            class="w-full rounded-control border border-line-strong bg-surface px-3 py-2 font-mono text-sm"
            :placeholder="t('studio.blocks.latexPlaceholder')"
            :aria-label="t('studio.blocks.latex')"
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
            class="h-9 w-full rounded-control border border-line-strong bg-surface px-3 text-sm"
            :placeholder="t('studio.blocks.altRequired')"
            :aria-label="t('studio.blocks.altText')"
            @input="updateData(index, { ...(block.data as any), alt: ($event.target as HTMLInputElement).value })"
          />
          <!-- Per-block, not per-asset, on purpose: the same image can carry different meaning in
               different articles, and the renderer prefers this over the asset's stored text. -->
          <p class="text-xs text-ink-subtle">
            {{ t("studio.blocks.altHintBefore") }} <em>{{ t("studio.blocks.altHintThis") }}</em>
            {{ t("studio.blocks.altHintAfter") }}
          </p>
        </template>

        <!-- embed -->
        <template v-else-if="block.type === 'embed'">
          <input
            :value="(block.data as any).url"
            class="h-9 w-full rounded-control border border-line-strong bg-surface px-3 text-sm"
            :placeholder="t('studio.blocks.embedPlaceholder')"
            :aria-label="t('studio.blocks.embedUrl')"
            @input="updateData(index, { ...(block.data as any), url: ($event.target as HTMLInputElement).value })"
          />
          <p class="text-xs text-ink-subtle">
            {{ t("studio.blocks.embedHint") }}
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
            {{ t("studio.blocks.numbered") }}
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
              :aria-label="t('studio.blocks.removeItem', { n: i + 1 })"
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
            {{ t("studio.blocks.addItem") }}
          </DbButton>
        </template>

        <!-- table -->
        <template v-else-if="block.type === 'table'">
          <div class="overflow-x-auto">
            <table class="w-full border-collapse text-sm">
              <thead>
                <tr>
                  <th
                    v-for="(header, c) in tableHeaders(block.data)"
                    :key="`h${c}`"
                    scope="col"
                    class="border border-line bg-surface-sunken p-1 align-top"
                  >
                    <RichTextEditor
                      :model-value="header"
                      placeholder="Heading"
                      @update:model-value="updateData(index, setHeader(block.data, c, $event))"
                    />
                  </th>
                  <th class="w-9 border-b border-line p-1 align-top">
                    <button
                      type="button"
                      class="h-8 w-8 rounded border border-line text-xs text-ink-muted hover:bg-surface-sunken"
                      :aria-label="t('studio.blocks.addColumn')"
                      title="Add column"
                      @click="updateData(index, addColumn(block.data))"
                    >
                      +
                    </button>
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, r) in tableRows(block.data)" :key="`r${r}`">
                  <td
                    v-for="(cellValue, c) in row"
                    :key="`r${r}c${c}`"
                    class="border border-line p-1 align-top"
                  >
                    <RichTextEditor
                      :model-value="cellValue"
                      @update:model-value="updateData(index, setCell(block.data, r, c, $event))"
                    />
                  </td>
                  <td class="p-1 align-top">
                    <button
                      type="button"
                      class="h-8 w-8 rounded border border-line text-xs text-danger hover:bg-danger-subtle"
                      :aria-label="t('studio.blocks.removeRow', { n: r + 1 })"
                      title="Remove row"
                      @click="updateData(index, removeRow(block.data, r))"
                    >
                      ✕
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="flex flex-wrap gap-2">
            <DbButton size="sm" variant="soft" @click="updateData(index, addRow(block.data))">
              {{ t("studio.blocks.addRow") }}
            </DbButton>
            <DbButton
              size="sm"
              variant="ghost"
              :disabled="tableColumnCount(block.data) <= 1"
              @click="updateData(index, removeColumn(block.data))"
            >
              {{ t("studio.blocks.removeLastColumn") }}
            </DbButton>
          </div>

          <p class="text-xs text-ink-subtle">
            {{ t("studio.blocks.tableHint") }}
            <code class="font-mono">{{ t("studio.blocks.inlineCode") }}</code>
            {{ t("studio.blocks.tableHintTail") }}
          </p>
        </template>

        <!-- divider has no data -->
        <template v-else-if="block.type === 'divider'">
          <p class="text-sm text-ink-subtle">{{ t("studio.blocks.noSettings") }}</p>
        </template>

        <template v-else>
          <p class="text-sm text-ink-subtle">
            {{ t("studio.blocks.noFormBefore") }} <code class="font-mono">{{ block.type }}</code>
            {{ t("studio.blocks.noFormAfter") }}
          </p>
        </template>
      </div>
    </div>

    <div class="flex flex-wrap gap-2 rounded-card border border-line bg-surface-sunken p-3">
      <span class="w-full text-xs font-medium uppercase tracking-wide text-ink-subtle">{{ t("studio.blocks.addBlock") }}</span>
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
