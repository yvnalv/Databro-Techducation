<script setup lang="ts">
import type { NuxtError } from "#app";

// Nuxt renders error.vue outside the layout, so the chrome is repeated minimally here.
const props = defineProps<{ error: NuxtError }>();

const { t } = useI18n();
const localePath = useLocalePath();

const isNotFound = computed(() => props.error?.statusCode === 404);
const title = computed(() => (isNotFound.value ? t("error.notFoundTitle") : t("error.genericTitle")));
const message = computed(() => (isNotFound.value ? t("error.notFoundMessage") : t("error.genericMessage")));

// Error pages must never be indexed, whatever status they carry.
useSeoMeta({ title: title.value, robots: "noindex,nofollow" });
</script>

<template>
  <div class="mx-auto max-w-shell px-6 py-24">
    <p class="text-sm font-semibold uppercase tracking-wide text-ink-subtle">{{ error?.statusCode }}</p>
    <h1 class="mt-3 font-display text-4xl font-bold tracking-tight">{{ title }}</h1>
    <p class="mt-4 text-lg text-ink-muted">{{ message }}</p>
    <p class="mt-8">
      <NuxtLink :to="localePath('/')" @click="clearError({ redirect: '/' })">{{ t("error.backHome") }}</NuxtLink>
    </p>
  </div>
</template>
