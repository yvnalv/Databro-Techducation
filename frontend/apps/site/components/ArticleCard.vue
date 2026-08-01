<script setup lang="ts">
import { DbCard, DbChip } from "@databro/ui";
import type { ArticleSummary } from "@databro/types";

/**
 * Article card (docs/UI_PATTERNS.md §2).
 *
 * Reference anatomy, top to bottom: cover, category chip, title, excerpt, then a footer row with
 * author avatar + name + date on the left and read time on the right.
 *
 * The cover is deliberately optional. Media does not exist yet, so the card has to look *designed*
 * without an image rather than broken — hence a tinted, initial-bearing panel instead of a grey
 * placeholder box. When Media lands, a real image replaces it and nothing else changes.
 */
const props = defineProps<{ article: ArticleSummary }>();

const { t, locale } = useI18n();
const localePath = useLocalePath();

const formattedDate = computed(() =>
  props.article.publishedAt
    ? new Date(props.article.publishedAt).toLocaleDateString(locale.value, {
        year: "numeric",
        month: "short",
        day: "numeric",
      })
    : "",
);

const authorName = computed(
  () => props.article.author?.displayName ?? t("articles.unknownAuthor"),
);

const authorInitial = computed(() => authorName.value.trim().charAt(0).toUpperCase() || "D");

// Deterministic tint from the slug, so a card keeps the same colour across pages and deploys
// rather than flickering between renders (UI_PATTERNS §5 applies the same rule to category tiles).
const COVER_TINTS = [
  "bg-accent-subtle text-accent",
  "bg-secondary-subtle text-secondary",
  "bg-info-subtle text-info",
  "bg-success-subtle text-success",
];

const coverTint = computed(() => {
  const seed = [...props.article.slug].reduce((sum, char) => sum + char.charCodeAt(0), 0);
  return COVER_TINTS[seed % COVER_TINTS.length];
});
</script>

<template>
  <DbCard as="article" :padded="false" interactive class="flex h-full flex-col overflow-hidden">
    <!-- Decorative: the title carries the meaning, so this is hidden from assistive tech. -->
    <div
      class="flex h-36 items-center justify-center font-display text-4xl font-bold"
      :class="coverTint"
      aria-hidden="true"
    >
      {{ article.title.trim().charAt(0).toUpperCase() }}
    </div>

    <div class="flex flex-1 flex-col p-5">
      <DbChip v-if="article.category" tone="category" class="self-start">
        {{ article.category.name }}
      </DbChip>

      <h3 class="mt-3 font-display text-lg font-semibold leading-snug tracking-tight text-ink">
        <!-- The card is not a link; the title is, so the accessible name is the title rather than
             the card's entire text content. -->
        <NuxtLink
          :to="localePath(`/articles/${article.slug}`)"
          class="transition-colors hover:text-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
        >
          {{ article.title }}
        </NuxtLink>
      </h3>

      <p class="mt-2 line-clamp-2 flex-1 text-sm leading-relaxed text-ink-muted">
        {{ article.summary }}
      </p>

      <div class="mt-5 flex items-center justify-between gap-3 border-t border-line pt-4">
        <div class="flex min-w-0 items-center gap-2.5">
          <span
            class="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-surface-sunken text-xs font-semibold text-ink-muted"
            aria-hidden="true"
          >
            {{ authorInitial }}
          </span>
          <span class="min-w-0">
            <span class="block truncate text-xs font-semibold text-ink">{{ authorName }}</span>
            <time
              v-if="article.publishedAt"
              :datetime="article.publishedAt"
              class="block text-xs text-ink-subtle"
            >
              {{ formattedDate }}
            </time>
          </span>
        </div>

        <span class="flex shrink-0 items-center gap-2">
          <DbChip v-if="article.visibility === 'premium'" tone="premium">
            {{ t("premium.badge") }}
          </DbChip>
          <span class="whitespace-nowrap text-xs text-ink-subtle">
            {{ t("articles.readingTime", { minutes: article.readingTimeMinutes }) }}
          </span>
        </span>
      </div>
    </div>
  </DbCard>
</template>
