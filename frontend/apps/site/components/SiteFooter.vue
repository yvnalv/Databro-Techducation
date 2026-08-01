<script setup lang="ts">
import type { Category } from "@databro/types";

/**
 * Site footer (docs/UI_PATTERNS.md §1.4).
 *
 * Dark even in light mode — it terminates the page, which is exactly what the reference uses it for.
 * The reference's five columns become four: DataBro has no apps, so the app-store column is dropped
 * rather than filled with placeholders.
 *
 * Categories are real links pulled from the API, not a hardcoded list: the footer is a genuine
 * crawl surface, and a stale hardcoded list would rot silently.
 */
const { t } = useI18n();
const localePath = useLocalePath();
const client = useApiClient();

const { data: categories } = await useAsyncData<Category[]>(
  "footer:categories",
  () => client.listCategories(),
  // The footer must never be the reason a page fails to render.
  { default: () => [] },
);

const topCategories = computed(() => (categories.value ?? []).slice(0, 5));
</script>

<template>
  <footer class="mt-24 bg-accent-deep text-white/70">
    <div class="mx-auto max-w-shell px-4 py-14 sm:px-6">
      <div class="grid gap-10 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <!-- White, not the brand blue: blue-on-navy is the one place the mark loses contrast. -->
          <NuxtLink :to="localePath('/')" class="inline-flex text-white">
            <BrandMark class="text-white [&>rect:first-child]:fill-white [&>span:last-child]:text-white" />
          </NuxtLink>
          <p class="mt-4 max-w-xs text-sm leading-relaxed text-white/65">
            {{ t("site.description") }}
          </p>
        </div>

        <div>
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-white">
            {{ t("footer.learn") }}
          </h2>
          <ul class="mt-4 space-y-2.5 text-sm">
            <li>
              <NuxtLink :to="localePath('/')" class="text-white/65 transition-colors hover:text-white">
                {{ t("nav.articles") }}
              </NuxtLink>
            </li>
          </ul>
        </div>

        <div>
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-white">
            {{ t("footer.topics") }}
          </h2>
          <ul v-if="topCategories.length" class="mt-4 space-y-2.5 text-sm">
            <li v-for="category in topCategories" :key="category.id">
              <NuxtLink
                :to="localePath(`/categories/${category.slug}`)"
                class="text-white/65 transition-colors hover:text-white"
              >
                {{ category.name }}
              </NuxtLink>
            </li>
          </ul>
          <p v-else class="mt-4 text-sm text-white/50">{{ t("footer.topicsEmpty") }}</p>
        </div>

        <div>
          <h2 class="font-display text-sm font-semibold uppercase tracking-wide text-white">
            {{ t("footer.contact") }}
          </h2>
          <ul class="mt-4 space-y-2.5 text-sm">
            <li>
              <a
                href="mailto:hello@databro.id"
                class="text-white/65 transition-colors hover:text-white"
              >
                hello@databro.id
              </a>
            </li>
          </ul>
        </div>
      </div>

      <div
        class="mt-12 flex flex-col gap-4 border-t border-white/10 pt-6 text-sm text-white/65 sm:flex-row sm:items-center sm:justify-between"
      >
        <p>&copy; {{ new Date().getFullYear() }} {{ t("site.name") }}. {{ t("footer.rights") }}</p>
      </div>
    </div>
  </footer>
</template>
