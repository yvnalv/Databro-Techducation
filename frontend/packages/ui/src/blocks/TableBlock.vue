<script setup lang="ts">
import type { TableBlock } from "@databro/types";

defineProps<{ data: TableBlock["data"] }>();
</script>

<template>
  <!-- Wrapped so a wide table scrolls itself instead of forcing the page to scroll sideways. -->
  <div class="overflow-x-auto">
    <table class="w-full">
      <thead v-if="data.headers.length">
        <tr>
          <th v-for="(header, index) in data.headers" :key="index" scope="col">{{ header }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, rowIndex) in data.rows" :key="rowIndex">
          <td v-for="(cell, cellIndex) in row" :key="cellIndex">{{ cell }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
