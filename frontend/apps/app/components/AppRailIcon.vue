<script setup lang="ts">
/**
 * The rail's icon set.
 *
 * Inline rather than an icon library: the rail needs nine glyphs, and a dependency that ships
 * hundreds to deliver nine is the wrong trade on a surface that renders on every authenticated
 * page. A real icon system is stage B of the UI rework (S-8) — when it lands, this component is
 * the single place that changes.
 *
 * Every path is drawn on a 24-grid with a 2px stroke so the set stays optically consistent, and
 * `currentColor` throughout so the rail's active/inactive states drive them.
 */
defineProps<{ name: string }>();
</script>

<template>
  <svg
    width="18"
    height="18"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
    class="shrink-0"
  >
    <template v-if="name === 'dashboard'">
      <rect x="3" y="3" width="7" height="7" rx="1.5" />
      <rect x="14" y="3" width="7" height="7" rx="1.5" />
      <rect x="3" y="14" width="7" height="7" rx="1.5" />
      <rect x="14" y="14" width="7" height="7" rx="1.5" />
    </template>

    <template v-else-if="name === 'browse'">
      <path d="M4 19.5V5a2 2 0 0 1 2-2h13v18H6a2 2 0 0 1-2-1.5z" />
      <path d="M8 7h7" />
    </template>

    <template v-else-if="name === 'articles'">
      <path d="M14 3v4a1 1 0 0 0 1 1h4" />
      <path d="M5 8V5a2 2 0 0 1 2-2h7l5 5v11a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2z" />
      <path d="M9 13h6M9 17h4" />
    </template>

    <template v-else-if="name === 'courses'">
      <path d="M12 6.5 3 4v14l9 2.5L21 18V4z" />
      <path d="M12 6.5v14" />
    </template>

    <template v-else-if="name === 'paths'">
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="18" cy="18" r="2.5" />
      <path d="M8.5 6H15a3 3 0 0 1 0 6H9a3 3 0 0 0 0 6h6.5" />
    </template>

    <template v-else-if="name === 'lessons'">
      <path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H19v15H6.5A2.5 2.5 0 0 0 4 20.5z" />
      <path d="M8 8h7" />
    </template>

    <template v-else-if="name === 'quizzes'">
      <circle cx="12" cy="12" r="9" />
      <path d="M9.5 9.5a2.6 2.6 0 1 1 3.4 2.5c-.6.2-.9.8-.9 1.4v.3" />
      <path d="M12 17.2h.01" />
    </template>

    <template v-else-if="name === 'taxonomy'">
      <path d="M4 6h16M4 12h10M4 18h6" />
    </template>

    <template v-else-if="name === 'site'">
      <circle cx="12" cy="12" r="9" />
      <path d="M3.5 9h17M3.5 15h17" />
      <path d="M12 3a15 15 0 0 1 0 18a15 15 0 0 1 0-18z" />
    </template>

    <template v-else-if="name === 'collapse'">
      <path d="M3 6h18M3 12h18M3 18h18" />
    </template>

    <!-- Falls through to nothing rather than to a placeholder glyph: an icon that silently renders
         the wrong picture is harder to notice than one that renders none. -->
  </svg>
</template>
