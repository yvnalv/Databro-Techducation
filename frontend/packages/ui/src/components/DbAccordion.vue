<script setup lang="ts">
import { computed, ref } from "vue";

/**
 * Accordion (docs/DESIGN_SYSTEM.md §5.6).
 *
 * Stacked cards with gaps rather than a bordered list, matching the reference. Headers are real
 * `<button>` elements with `aria-expanded` and `aria-controls` — a clickable `<div>` is neither
 * focusable nor announced as interactive.
 */
export interface AccordionItem {
  id: string;
  question: string;
  answer: string;
}

const props = withDefaults(
  defineProps<{
    items: AccordionItem[];
    /** Allow several panels open at once. Single-open by default, as in the reference. */
    multiple?: boolean;
    /** Id of the panel open on first render. */
    defaultOpen?: string;
  }>(),
  { multiple: false },
);

const open = ref<string[]>(props.defaultOpen ? [props.defaultOpen] : []);

const isOpen = (id: string) => open.value.includes(id);

function toggle(id: string) {
  if (isOpen(id)) {
    open.value = open.value.filter((value) => value !== id);
    return;
  }
  open.value = props.multiple ? [...open.value, id] : [id];
}

const panelId = (id: string) => `db-accordion-panel-${id}`;
const headerId = (id: string) => `db-accordion-header-${id}`;
const chevron = computed(() => "h-4 w-4 shrink-0 transition-transform duration-150");
</script>

<template>
  <div class="space-y-2">
    <div v-for="item in items" :key="item.id" class="rounded-card border border-line bg-surface-raised">
      <h3>
        <button
          :id="headerId(item.id)"
          type="button"
          :aria-expanded="isOpen(item.id)"
          :aria-controls="panelId(item.id)"
          class="flex w-full items-center justify-between gap-4 px-5 py-4 text-left text-base font-semibold text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          @click="toggle(item.id)"
        >
          {{ item.question }}
          <svg
            :class="[chevron, isOpen(item.id) ? 'rotate-180' : '']"
            viewBox="0 0 20 20"
            fill="none"
            aria-hidden="true"
          >
            <path d="M5 7.5 10 12.5 15 7.5" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </button>
      </h3>

      <div
        v-show="isOpen(item.id)"
        :id="panelId(item.id)"
        role="region"
        :aria-labelledby="headerId(item.id)"
        class="border-t border-line px-5 py-4 text-sm text-ink-muted"
      >
        {{ item.answer }}
      </div>
    </div>
  </div>
</template>
