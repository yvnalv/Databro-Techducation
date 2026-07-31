import type { Component } from "vue";
import type { BlockType } from "@databro/types";

import CalloutBlock from "./CalloutBlock.vue";
import CodeBlock from "./CodeBlock.vue";
import DividerBlock from "./DividerBlock.vue";
import EmbedBlock from "./EmbedBlock.vue";
import HeadingBlock from "./HeadingBlock.vue";
import ImageBlock from "./ImageBlock.vue";
import ListBlock from "./ListBlock.vue";
import MathBlock from "./MathBlock.vue";
import ParagraphBlock from "./ParagraphBlock.vue";
import QuoteBlock from "./QuoteBlock.vue";
import TableBlock from "./TableBlock.vue";

/**
 * The block renderer registry (docs/CONTENT_MODEL.md).
 *
 * Lives in @databro/ui rather than in the site app because both surfaces render the same
 * documents: the public site and the CMS preview in `app`. One registry is what keeps "what the
 * author sees" and "what the reader gets" from drifting.
 *
 * Typed as Record<BlockType, Component>, so adding a member to BlockType fails the build here
 * until a renderer exists for it.
 */
export const blockRegistry: Record<BlockType, Component> = {
  heading: HeadingBlock,
  paragraph: ParagraphBlock,
  code: CodeBlock,
  callout: CalloutBlock,
  image: ImageBlock,
  quote: QuoteBlock,
  list: ListBlock,
  divider: DividerBlock,
  embed: EmbedBlock,
  table: TableBlock,
  math: MathBlock,
};

export function resolveBlockComponent(type: string): Component | null {
  return Object.hasOwn(blockRegistry, type) ? blockRegistry[type as BlockType] : null;
}
