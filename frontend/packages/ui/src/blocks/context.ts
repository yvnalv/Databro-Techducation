import type { InjectionKey } from "vue";
import type { MediaRef } from "@databro/types";
import { codeKey } from "./code-key";

/**
 * Resolves an ImageBlock's `mediaId` to a renderable asset (ADR-0011).
 *
 * Returns the whole {@link MediaRef}, not just a URL, so the block can emit a `srcset` from the
 * asset's variants. Null means the id cannot be resolved — an asset that was deleted, or one whose
 * host has supplied no resolver — and the block falls back to a placeholder rather than a broken
 * image.
 */
export type MediaResolver = (mediaId: string) => MediaRef | null;

export interface RendererOptions {
  /**
   * Render a visible placeholder for block types this build does not know about.
   *
   * Content outlives renderers: a JSONB document may carry a type added after this bundle
   * shipped. The public site hides them (a reader should never see scaffolding), while the CMS
   * preview shows them so an author can tell something is there.
   */
  showUnknownBlocks: boolean;
}

export const mediaResolverKey: InjectionKey<MediaResolver> = Symbol("databro.mediaResolver");
export const rendererOptionsKey: InjectionKey<RendererOptions> = Symbol("databro.rendererOptions");

/**
 * Current block nesting depth. List items may contain blocks (ADR-0009), which makes the renderer
 * recursive, so a malformed or hostile document could otherwise nest deeply enough to exhaust the
 * stack during SSR — taking down the request, not just the block.
 */
export const nestingDepthKey: InjectionKey<number> = Symbol("databro.nestingDepth");

/**
 * One level of nesting is what the content model is for — a step containing a code sample, or a
 * two-level list. Deeper than that is almost certainly a malformed document, so nested blocks are
 * dropped rather than rendered.
 */
export const MAX_NESTING_DEPTH = 1;

/**
 * Returns pre-highlighted HTML for a code sample, or null to render it as plain text.
 *
 * Highlighting happens on the server and travels in the page payload; this is a lookup, not a
 * computation. That is deliberate: a highlighter is a large dependency and a reader should not
 * download a grammar set to read a page that was already rendered for them.
 */
export type CodeHighlighter = (code: string, language: string) => string | null;

export const codeHighlighterKey: InjectionKey<CodeHighlighter> = Symbol("databro.codeHighlighter");

export const defaultCodeHighlighter: CodeHighlighter = () => null;

// Re-exported for renderer consumers. The definition lives in its own dependency-free module so the
// site's Nitro server can import it without dragging this file's Vue dependency — and through it
// the whole component library — into a server bundle Rollup cannot parse.
export { codeKey };

/** Builds a highlighter over a map produced server-side by {@link codeKey}. */
export function codeHighlighterFor(
  highlighted: Record<string, string> | undefined | null,
): CodeHighlighter {
  return (code: string, language: string) => highlighted?.[codeKey(code, language)] ?? null;
}

export const defaultMediaResolver: MediaResolver = () => null;

/**
 * Builds a resolver over the `media` map the API ships with an article.
 *
 * The map is keyed by media id, which is exactly what a block's `mediaId` holds — so this is a
 * lookup, not a fetch. That is the point: resolving media client-side would mean a request per
 * image on the cached read path.
 */
export function mediaResolverFor(
  media: Record<string, MediaRef> | undefined | null,
): MediaResolver {
  return (mediaId: string) => media?.[mediaId] ?? null;
}
export const defaultRendererOptions: RendererOptions = { showUnknownBlocks: false };
