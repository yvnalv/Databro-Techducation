<script setup lang="ts">
import type { Category } from "@databro/types";

/**
 * Category tiles (docs/UI_PATTERNS.md §5).
 *
 * Reference anatomy: a grid of tinted tiles, each with an icon, a name and a count. The tints come
 * from the `50` step of the palette hues rather than arbitrary pastels (DESIGN_SYSTEM §1.6), and are
 * assigned **deterministically from the slug** so a category keeps its colour across pages and
 * between deploys instead of shuffling on every render.
 *
 * Shows **any category that has published articles of its own**, at whatever depth — not just
 * top-level ones. Two rejected alternatives explain why:
 *
 *   - *Top-level only* hides everything when the articles live in child categories, which is the
 *     normal shape of a growing taxonomy (a broad parent, specific children).
 *   - *Top-level with counts rolled up from descendants* would make a tile promise 28 articles and
 *     the page it links to show 0, because the category page filters strictly by that category.
 *     A count must always agree with the page it points at.
 *
 * Rolling up would first require the category page itself to include descendants — a deliberate
 * change to what a category *means*, not a display tweak.
 */
const props = defineProps<{ categories: Category[] }>();

const { t } = useI18n();
const localePath = useLocalePath();

const TINTS = [
  "bg-accent-subtle text-accent",
  "bg-secondary-subtle text-secondary",
  "bg-info-subtle text-info",
  "bg-success-subtle text-success",
  "bg-warning-subtle text-warning",
];

const tileTint = (slug: string) => {
  const seed = [...slug].reduce((sum, char) => sum + char.charCodeAt(0), 0);
  return TINTS[seed % TINTS.length];
};

// Empty categories are hidden: a tile promising "0 articles" is an invitation to a dead end.
const populated = computed(() =>
  [...props.categories]
    .filter((c) => c.articleCount > 0)
    .sort((a, b) => b.articleCount - a.articleCount || a.name.localeCompare(b.name)),
);
</script>

<template>
  <section v-if="populated.length" class="border-b border-line bg-surface">
    <div class="db-shell py-16 sm:py-20">
      <div class="text-center">
        <h2 class="font-display text-3xl font-bold tracking-tight text-ink">
          {{ t("home.categoriesTitle") }}
        </h2>
        <p class="mx-auto mt-3 max-w-2xl text-ink-muted">{{ t("home.categoriesSubtitle") }}</p>
      </div>

      <ul class="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        <li v-for="category in populated" :key="category.id">
          <NuxtLink
            :to="localePath(`/categories/${category.slug}`)"
            class="group flex h-full items-center gap-4 rounded-card border border-line bg-surface-raised p-5 transition-shadow hover:shadow-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
          >
            <span
              class="flex h-12 w-12 shrink-0 items-center justify-center rounded-card font-display text-lg font-bold"
              :class="tileTint(category.slug)"
              aria-hidden="true"
            >
              {{ category.name.trim().charAt(0).toUpperCase() }}
            </span>

            <span class="min-w-0">
              <span
                class="block truncate font-display font-semibold text-ink transition-colors group-hover:text-accent"
              >
                {{ category.name }}
              </span>
              <span class="mt-0.5 block text-sm text-ink-subtle">
                {{ t("categories.articleCount", category.articleCount) }}
              </span>
            </span>
          </NuxtLink>
        </li>
      </ul>
    </div>
  </section>
</template>
