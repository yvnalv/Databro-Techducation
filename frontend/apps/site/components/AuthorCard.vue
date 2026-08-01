<script setup lang="ts">
import { DbCard } from "@databro/ui";
import type { AuthorProfile } from "@databro/types";

/**
 * Author card (docs/UI_PATTERNS.md §3).
 *
 * The reference centres a large avatar with social links beneath. DataBro runs it horizontally:
 * there are no social profiles to link yet, and a horizontal card takes less vertical space between
 * the end of the article and the related links, which is where a reader should go next.
 *
 * Renders nothing at all when there is no bio — an author card with a name and empty space is worse
 * than no card.
 */
const props = defineProps<{ author: AuthorProfile }>();

const { t } = useI18n();

const bio = computed(() => props.author.bio?.trim() || null);
const initial = computed(() => props.author.displayName.trim().charAt(0).toUpperCase() || "D");
</script>

<template>
  <DbCard v-if="bio" as="aside" :aria-label="t('articles.aboutAuthor')" class="flex gap-4">
    <img
      v-if="author.avatarUrl"
      :src="author.avatarUrl"
      alt=""
      class="h-14 w-14 shrink-0 rounded-full object-cover"
      loading="lazy"
    />
    <!-- Decorative: the author's name is already in the text beside it. -->
    <span
      v-else
      class="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-accent-subtle font-display text-lg font-bold text-accent"
      aria-hidden="true"
    >
      {{ initial }}
    </span>

    <div class="min-w-0">
      <p class="text-xs font-medium uppercase tracking-wide text-ink-subtle">
        {{ t("articles.aboutAuthor") }}
      </p>
      <p class="mt-0.5 font-display text-base font-semibold text-ink">
        {{ author.displayName }}
      </p>
      <p class="mt-2 text-sm leading-relaxed text-ink-muted">{{ bio }}</p>
    </div>
  </DbCard>
</template>
