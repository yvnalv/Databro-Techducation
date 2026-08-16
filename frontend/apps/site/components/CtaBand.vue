<script setup lang="ts">
/**
 * Full-width CTA band (docs/UI_PATTERNS.md §1.3).
 *
 * The reference puts a newsletter capture here on every page. DataBro does **not** ship the form:
 * no newsletter provider has been chosen yet (it is an open question in STATUS.md), and a subscribe
 * field that silently discards addresses is worse than no field — it costs trust the first time
 * someone notices they never received anything.
 *
 * So the band keeps the reference's rhythm and weight but carries a real link instead of a fake
 * form. Swap in the capture form once a provider exists.
 */
const { t } = useI18n();
const localePath = useLocalePath();
</script>

<template>
  <!-- Solid brand blue with a navy action, as in the reference's newsletter band. -->
  <section class="bg-accent">
    <div class="db-shell py-16 text-center sm:py-20">
      <h2 class="font-display text-3xl font-bold tracking-tight text-white">
        {{ t("home.ctaTitle") }}
      </h2>
      <p class="mx-auto mt-3 max-w-xl text-white/80">{{ t("home.ctaSubtitle") }}</p>

      <div class="mt-8">
        <NuxtLink
          :to="localePath('/')"
          class="inline-flex h-12 items-center justify-center rounded-full bg-accent-deep px-8 text-base font-semibold text-white transition-opacity hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white focus-visible:ring-offset-2 focus-visible:ring-offset-accent"
        >
          {{ t("home.ctaButton") }}
        </NuxtLink>
      </div>
    </div>
  </section>
</template>
