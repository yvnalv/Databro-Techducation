import type { InjectionKey } from "vue";

/**
 * Resolves an ImageBlock's `mediaId` to a URL. The Media module does not exist yet, so the
 * default resolver returns null and images render as a placeholder. When Media lands, only this
 * function changes - no block component does.
 */
export type MediaResolver = (mediaId: string) => string | null;

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

export const defaultMediaResolver: MediaResolver = () => null;
export const defaultRendererOptions: RendererOptions = { showUnknownBlocks: false };
