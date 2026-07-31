<script setup lang="ts">
import { computed, useId } from "vue";

/**
 * Text input with label and error (docs/DESIGN_SYSTEM.md §5.2).
 *
 * The error is wired through `aria-describedby` + `aria-invalid`, not just coloured — a red border
 * alone is invisible to a screen reader and to anyone who cannot distinguish the hue
 * (DESIGN_SYSTEM §6).
 */
const props = withDefaults(
  defineProps<{
    modelValue?: string;
    label?: string;
    /** Hidden visually but still announced — for inputs whose purpose is obvious from context. */
    labelHidden?: boolean;
    type?: string;
    placeholder?: string;
    error?: string;
    hint?: string;
    disabled?: boolean;
    required?: boolean;
  }>(),
  { type: "text", labelHidden: false, disabled: false, required: false },
);

defineEmits<{ "update:modelValue": [value: string] }>();

const uid = useId();
const inputId = computed(() => `db-input-${uid}`);
const errorId = computed(() => `${inputId.value}-error`);
const hintId = computed(() => `${inputId.value}-hint`);

const describedBy = computed(() => {
  const ids = [props.hint ? hintId.value : null, props.error ? errorId.value : null].filter(Boolean);
  return ids.length ? ids.join(" ") : undefined;
});
</script>

<template>
  <div>
    <label
      v-if="label"
      :for="inputId"
      :class="labelHidden ? 'sr-only' : 'mb-1.5 block text-sm font-medium text-ink-muted'"
    >
      {{ label }}
      <span v-if="required" class="text-danger" aria-hidden="true">*</span>
    </label>

    <div class="relative">
      <span
        v-if="$slots.leading"
        class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-ink-subtle"
      >
        <slot name="leading" />
      </span>

      <input
        :id="inputId"
        :type="type"
        :value="modelValue"
        :placeholder="placeholder"
        :disabled="disabled"
        :required="required"
        :aria-invalid="error ? 'true' : undefined"
        :aria-describedby="describedBy"
        class="h-10 w-full rounded-md border bg-surface px-3 text-sm text-ink transition-colors placeholder:text-ink-subtle focus:outline-none focus:ring-2 disabled:opacity-50"
        :class="[
          error
            ? 'border-danger focus:border-danger focus:ring-danger/25'
            : 'border-line-strong focus:border-accent focus:ring-accent/25',
          $slots.leading ? 'pl-9' : '',
        ]"
        @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
    </div>

    <p v-if="hint && !error" :id="hintId" class="mt-1.5 text-xs text-ink-subtle">{{ hint }}</p>
    <p v-if="error" :id="errorId" class="mt-1.5 text-xs text-danger">{{ error }}</p>
  </div>
</template>
