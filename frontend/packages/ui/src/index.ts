// Shared design system for the DataBro apps.
//
// Phase 1 exposes the Tailwind preset / design tokens. Vue components and the content-block
// renderer registry (docs/CONTENT_MODEL.md — one renderer used by both `site` and the CMS preview
// in `app`) are added here as they are built, so preview and production never diverge.

export { default as tailwindPreset, tokens } from "./tailwind-preset";

import type { BlockType } from "@databro/types";

/** The block types the renderer registry is expected to cover in Phase 1. */
export const SUPPORTED_BLOCK_TYPES: readonly BlockType[] = [
  "heading",
  "paragraph",
  "code",
  "callout",
  "image",
  "quote",
  "list",
  "divider",
  "embed",
  "table",
];
