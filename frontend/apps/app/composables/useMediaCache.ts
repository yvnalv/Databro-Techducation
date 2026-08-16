import type { MediaAsset, MediaRef } from "@databro/types";

/**
 * Assets seen during this editing session, so the live preview can render an image the moment it is
 * picked.
 *
 * The article's own `media` map only covers what was there when it was last loaded from the API — an
 * image uploaded thirty seconds ago is not in it, and the preview would show a placeholder until
 * save-and-reload. The picker writes into this cache; the preview reads through it.
 *
 * App-level state (`useState`) rather than a module-level object so it does not leak across requests
 * during SSR.
 */
export function useMediaCache() {
  const cache = useState<Record<string, MediaRef>>("databro:media-cache", () => ({}));

  function remember(asset: MediaAsset) {
    cache.value = {
      ...cache.value,
      [asset.id]: {
        url: asset.url,
        altText: asset.altText,
        width: asset.width,
        height: asset.height,
        variants: asset.variants,
      },
    };
  }

  /**
   * Merges a saved article's media map under the session cache. The cache wins: it holds the fresher
   * copy of anything just uploaded, including variants that arrived after the article was loaded.
   */
  function merged(articleMedia?: Record<string, MediaRef> | null): Record<string, MediaRef> {
    return { ...(articleMedia ?? {}), ...cache.value };
  }

  return { cache, remember, merged };
}
