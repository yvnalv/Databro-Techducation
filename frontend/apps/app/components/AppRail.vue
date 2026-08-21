<script setup lang="ts">
/**
 * The navigation rail — a floating dark panel inset from the frame edge, collapsible to icons.
 *
 * App chrome rather than a `@databro/ui` primitive: it needs `NuxtLink`, the current route and the
 * i18n layer, none of which belong in a package that must stay framework-agnostic.
 *
 * Used by **both** shells. The learner shell previously carried a top bar, on the reasoning that a
 * learner has two or three destinations and an editor has a dozen, so a permanent 240px rail would
 * be mostly empty for the learner. A rail that collapses to 68px answers that objection directly,
 * and one navigation model across the app beats two that have to be kept in agreement.
 */
export interface RailItem {
  label: string;
  icon: string;
  /** Internal route. Mutually exclusive with `href`. */
  to?: string;
  /** External destination — the public site. Rendered as a plain anchor, not a router link. */
  href?: string;
  /** Match this route and everything beneath it. Exact match when false. */
  prefix?: boolean;
  badge?: number | string | null;
}

export interface RailGroup {
  /** Section label. Hidden when collapsed, along with every other piece of text. */
  label?: string;
  items: RailItem[];
}

const props = defineProps<{
  groups: RailGroup[];
  collapsed: boolean;
  /** Accessible names for the collapse control, in both states. */
  labels: { nav: string; collapse: string; expand: string };
}>();

const emit = defineEmits<{ "update:collapsed": [value: boolean] }>();

const route = useRoute();

const isActive = (item: RailItem) => {
  if (!item.to) return false;
  return item.prefix ? route.path.startsWith(item.to) : route.path === item.to;
};

const visibleGroups = computed(() => props.groups.filter((g) => g.items.length > 0));
</script>

<template>
  <nav
    :aria-label="labels.nav"
    class="flex flex-col gap-6 rounded-panel bg-surface-inverse p-3 text-ink-inverted transition-[width] duration-200"
    :class="collapsed ? 'w-[68px]' : 'w-60'"
  >
    <div class="flex items-center gap-2" :class="collapsed ? 'justify-center' : 'px-2'">
      <NuxtLink to="/" class="flex min-w-0 items-center rounded-control text-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
        <AppBrandMark :with-wordmark="!collapsed" tone="inverted" />
      </NuxtLink>
    </div>

    <button
      type="button"
      :aria-label="collapsed ? labels.expand : labels.collapse"
      :aria-expanded="!collapsed"
      class="flex items-center gap-3 rounded-control p-2.5 text-sm font-medium text-ink-inverted/70 transition-colors hover:bg-ink-inverted/10 hover:text-ink-inverted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
      :class="collapsed ? 'justify-center' : ''"
      @click="emit('update:collapsed', !collapsed)"
    >
      <AppRailIcon name="collapse" />
      <span v-if="!collapsed" class="truncate">{{ labels.collapse }}</span>
    </button>

    <div v-for="(group, gi) in visibleGroups" :key="gi" class="flex flex-col gap-1">
      <!-- The section label is the first thing to go when collapsed: at 68px there is no room, and
           an abbreviated heading reads as a broken one. -->
      <p
        v-if="group.label && !collapsed"
        class="px-2.5 pb-1 text-[11px] font-semibold uppercase tracking-[0.09em] text-ink-inverted/40"
      >
        {{ group.label }}
      </p>

      <template v-for="item in group.items" :key="item.label">
        <component
          :is="item.href ? 'a' : resolveComponent('NuxtLink')"
          v-bind="item.href ? { href: item.href } : { to: item.to }"
          :aria-current="isActive(item) ? 'page' : undefined"
          :title="collapsed ? item.label : undefined"
          class="flex items-center gap-3 rounded-control p-2.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
          :class="[
            collapsed ? 'justify-center' : '',
            isActive(item)
              ? 'bg-accent font-semibold text-accent-on'
              : 'text-ink-inverted/70 hover:bg-ink-inverted/10 hover:text-ink-inverted',
          ]"
        >
          <AppRailIcon :name="item.icon" />
          <span v-if="!collapsed" class="min-w-0 flex-1 truncate">{{ item.label }}</span>
          <!-- Lime on the dark rail: 15.9:1, and the one place in light mode it is legible as a
               fill with dark text on it (ADR-0020 §3). -->
          <span
            v-if="!collapsed && item.badge"
            class="rounded-full bg-secondary px-2 py-px text-[11px] font-semibold tabular-nums text-secondary-on"
          >
            {{ item.badge }}
          </span>
        </component>
      </template>
    </div>

    <div class="mt-auto flex flex-col gap-1">
      <slot name="footer" :collapsed="collapsed" />
    </div>
  </nav>
</template>
