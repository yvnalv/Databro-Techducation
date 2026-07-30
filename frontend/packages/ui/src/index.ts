// Shared design system for the DataBro apps.
//
// Phase 1 exposes the Tailwind preset / design tokens and the content-block renderer registry
// (docs/CONTENT_MODEL.md) — one renderer used by both `site` and the CMS preview in `app`, so
// preview and production never diverge.

export { default as tailwindPreset, tokens } from "./tailwind-preset";

// ---- Content block renderer ----

export { default as ContentRenderer } from "./blocks/ContentRenderer.vue";
export { default as BlockRenderer } from "./blocks/BlockRenderer.vue";
export { blockRegistry, resolveBlockComponent } from "./blocks/registry";
export { resolveEmbed, isSafeLink, type EmbedTarget } from "./blocks/embed-providers";
export {
  mediaResolverKey,
  rendererOptionsKey,
  defaultMediaResolver,
  defaultRendererOptions,
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
