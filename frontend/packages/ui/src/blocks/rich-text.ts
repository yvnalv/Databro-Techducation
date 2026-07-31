import type { InlineMark, InlineNode, RichText } from "@databro/types";

/**
 * Normalizes a block's inline content.
 *
 * Accepts either the ADR-0009 node array or a legacy plain `text` string. Documents written before
 * ADR-0009 carry the string form; there is no production content to migrate, so the renderers
 * tolerate it rather than a data migration being written. Anything unrecognised yields no content
 * instead of throwing — content outlives renderers.
 */
export function toRichText(content: unknown, legacyText?: unknown): RichText {
  if (Array.isArray(content)) {
    return content.filter(isInlineNode);
  }
  if (typeof legacyText === "string" && legacyText.length > 0) {
    return [{ type: "text", text: legacyText }];
  }
  return [];
}

function isInlineNode(value: unknown): value is InlineNode {
  if (typeof value !== "object" || value === null) return false;
  const node = value as { type?: unknown; text?: unknown; attrs?: unknown };

  if (node.type === "text") return typeof node.text === "string";
  if (node.type === "mathInline") {
    return typeof (node.attrs as { latex?: unknown } | undefined)?.latex === "string";
  }
  return false;
}

/** Plain-text projection, for `alt`, `title`, and other attribute contexts. */
export function richTextToPlain(content: RichText): string {
  return content
    .map((node) => (node.type === "text" ? node.text : node.attrs.latex))
    .join("");
}

const MARK_ELEMENTS: Record<Exclude<InlineMark["type"], "link">, string> = {
  bold: "strong",
  italic: "em",
  code: "code",
  strike: "s",
};

export type MarkElement =
  | { tag: string; attrs?: Record<string, string> }
  | null;

/**
 * Maps a mark to the element that should wrap the text.
 *
 * Marks become elements, never HTML strings — the no-`v-html` rule from the block renderer applies
 * just as much to inline content, which is equally author-supplied.
 *
 * A `link` whose href is not http(s) yields no element at all: the text still renders, but it is not
 * a link. That blocks `javascript:` and `data:` URLs, which are the reason this function exists.
 */
export function markToElement(mark: InlineMark): MarkElement {
  if (mark.type === "link") {
    const href = safeHref(mark.attrs?.href);
    if (!href) return null;

    return {
      tag: "a",
      attrs: {
        href,
        // Author-supplied outbound links: don't pass referrer or window handles, and don't lend
        // ranking weight to arbitrary destinations.
        rel: "nofollow noopener noreferrer",
        ...(mark.attrs.title ? { title: mark.attrs.title } : {}),
      },
    };
  }

  const tag = MARK_ELEMENTS[mark.type];
  return tag ? { tag } : null;
}

/** Allows absolute http(s) and site-relative URLs; rejects every other scheme. */
export function safeHref(href: unknown): string | null {
  if (typeof href !== "string") return null;

  const trimmed = href.trim();
  if (trimmed.length === 0) return null;

  // Site-relative links (internal linking between articles) never carry a scheme.
  if (trimmed.startsWith("/") && !trimmed.startsWith("//")) return trimmed;

  try {
    const { protocol } = new URL(trimmed);
    return protocol === "https:" || protocol === "http:" ? trimmed : null;
  } catch {
    return null;
  }
}
