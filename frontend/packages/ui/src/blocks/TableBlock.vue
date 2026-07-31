<script setup lang="ts">
import { computed } from "vue";
import type { TableBlock } from "@databro/types";
import RichText from "./RichText";
import { toRichText } from "./rich-text";

const props = defineProps<{ data: TableBlock["data"] }>();

// Cells carry inline content (ADR-0009) — comparison tables in technical writing lean heavily on
// `inline code` and links. A plain string is the pre-ADR-0009 form.
const cell = (value: unknown) => (typeof value === "string" ? toRichText(null, value) : toRichText(value));

const headers = computed(() => (props.data?.headers ?? []).map(cell));
const rows = computed(() => (props.data?.rows ?? []).map((row) => (row ?? []).map(cell)));
</script>

<template>
  <!-- Wrapped so a wide table scrolls itself instead of forcing the page to scroll sideways. -->
  <div class="overflow-x-auto">
    <table class="w-full">
      <thead v-if="headers.length">
        <tr>
          <th v-for="(header, index) in headers" :key="index" scope="col">
            <RichText :content="header" />
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, rowIndex) in rows" :key="rowIndex">
          <td v-for="(value, cellIndex) in row" :key="cellIndex">
            <RichText :content="value" />
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
