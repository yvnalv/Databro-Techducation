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

export const defaultMediaResolver: MediaResolver = () => null;
export const defaultRendererOptions: RendererOptions = { showUnknownBlocks: false };
