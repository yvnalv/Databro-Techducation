<script setup lang="ts">
import type { ListBlock } from "@databro/types";

defineProps<{ data: ListBlock["data"] }>();
</script>

<template>
  <component :is="data.ordered ? 'ol' : 'ul'" :class="data.ordered ? 'list-decimal' : 'list-disc'" class="pl-6">
    <li v-for="(item, index) in data.items" :key="index">{{ item }}</li>
  </component>
</template>
