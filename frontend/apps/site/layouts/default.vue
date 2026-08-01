<script setup lang="ts">
// Site chrome. Every string goes through i18n (CLAUDE.md rule 19) - no literals in the template.
const { t } = useI18n();
</script>

<template>
  <div class="flex min-h-screen flex-col bg-surface font-sans text-ink antialiased">
    <!-- Keyboard users land here first; visible only when focused. -->
    <a
      href="#main"
      class="sr-only focus:not-sr-only focus:absolute focus:z-50 focus:m-3 focus:rounded-md focus:bg-accent focus:px-4 focus:py-2 focus:text-ink-inverted"
    >
      {{ t("nav.skipToContent") }}
    </a>

    <SiteHeader />

    <main id="main" class="flex-1">
      <slot />
    </main>

    <SiteFooter />
  </div>
</template>
