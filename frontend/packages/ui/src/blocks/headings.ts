import type { ContentDocument, HeadingBlock } from "@databro/types";

/**
 * Slugifies heading text into a stable anchor id.
 *
 * Exported and shared on purpose: the renderer stamps this id onto the heading, and a table of
 * contents links to it. Two independent implementations would drift on the first edge case
 * (punctuation, casing, an emoji) and every TOC link would silently scroll nowhere.
 */
export function headingAnchor(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, "")
    .trim()
    .replace(/\s+/g, "-");
}

export interface TocEntry {
  id: string;
  text: string;
  level: 2 | 3;
}

/**
 * Extracts a table of contents from a content document.
 *
 * Only `h2` and `h3`: an `h4` is usually a detail inside a subsection, and including it turns the
 * contents into an outline of the whole article rather than a way to navigate it. Entries with no
 * usable anchor (heading text that slugifies to nothing) are dropped rather than rendered as dead
 * links.
 */
export function buildToc(document: ContentDocument | undefined): TocEntry[] {
  if (!document?.blocks) return [];

  return document.blocks
    .filter((block): block is HeadingBlock => block.type === "heading")
    // Exclude rather than coerce: an h4 flattened to h2 would sit in the contents claiming to be a
    // top-level section it is not.
    .filter((block) => block.data?.level === 2 || block.data?.level === 3)
    .map((block) => {
      const text = block.data?.text ?? "";
      return { id: headingAnchor(text), text, level: block.data.level as 2 | 3 };
    })
    .filter((entry) => entry.id.length > 0 && entry.text.length > 0);
}
