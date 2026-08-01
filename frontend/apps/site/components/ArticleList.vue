<script setup lang="ts">
import type { ArticleSummary } from "@databro/types";

// Shared by the homepage, category pages and tag pages so a change to the card lands everywhere.
defineProps<{ articles: ArticleSummary[] }>();

const { t } = useI18n();
</script>

<template>
  <p v-if="!articles.length" class="mt-8 text-ink-muted">{{ t("articles.listEmpty") }}</p>

  <!-- 3-up on the wide shell container, per the reference grid (UI_PATTERNS §2). -->
  <ul v-else class="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
    <li v-for="article in articles" :key="article.id">
      <ArticleCard :article="article" />
    </li>
  </ul>
</template>
