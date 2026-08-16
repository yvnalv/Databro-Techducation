// Shared design system for the DataBro apps.
//
// Phase 1 exposes the Tailwind preset / design tokens and the content-block renderer registry
// (docs/CONTENT_MODEL.md) — one renderer used by both `site` and the CMS preview in `app`, so
// preview and production never diverge.

export { default as tailwindPreset, tokens } from "./tailwind-preset";

// ---- Primitives (docs/DESIGN_SYSTEM.md §5) ----

export { default as DbButton } from "./components/DbButton.vue";
export { default as DbCard } from "./components/DbCard.vue";
export { default as DbChip } from "./components/DbChip.vue";
export { default as DbInput } from "./components/DbInput.vue";
export { default as DbAccordion } from "./components/DbAccordion.vue";
export type { AccordionItem } from "./components/DbAccordion.vue";

// ---- Content block renderer ----

export { default as ContentRenderer } from "./blocks/ContentRenderer.vue";
export { default as BlockRenderer } from "./blocks/BlockRenderer.vue";
export { default as RichText } from "./blocks/RichText";
export { blockRegistry, resolveBlockComponent } from "./blocks/registry";
export { resolveEmbed, isSafeLink, type EmbedTarget } from "./blocks/embed-providers";
export { toRichText, richTextToPlain, markToElement, safeHref } from "./blocks/rich-text";
export { headingAnchor, buildToc, type TocEntry } from "./blocks/headings";
export { renderMath } from "./blocks/katex";
export {
  mediaResolverKey,
  rendererOptionsKey,
  nestingDepthKey,
  defaultMediaResolver,
  mediaResolverFor,
  defaultRendererOptions,
  MAX_NESTING_DEPTH,
  type MediaResolver,
  type RendererOptions,
} from "./blocks/context";

import { blockRegistry } from "./blocks/registry";
import type { BlockType } from "@databro/types";

/**
 * The block types this build can render. Derived from the registry rather than hand-maintained,
 * so it cannot drift from what is actually implemented.
 */
export const SUPPORTED_BLOCK_TYPES: readonly BlockType[] = Object.keys(blockRegistry) as BlockType[];
