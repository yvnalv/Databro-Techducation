import type { ApiClient } from "@databro/api-client";
import type { NuxtApp } from "#app";

interface RedirectContext {
  /** Captured before the caller's first `await`, so it can restore Nuxt context afterwards. */
  nuxtApp: NuxtApp;
  client: ApiClient;
  localePath: (path: string) => string;
}

/**
 * Honors a moved slug (docs/SEO.md §4).
 *
 * A content page calls this when its API read 404s, passing the canonical (locale-free) path the
 * resource would live at — e.g. `/articles/old-slug`. If a redirect exists, the visitor is sent to
 * the new path with the stored status (301):
 *  - On the server `navigateTo` with `redirectCode` sends the 301 and aborts the render — the
 *    SEO-critical path, and the only one a crawler ever sees.
 *  - On a client-side navigation to a stale internal link it performs the navigation.
 *
 * When no redirect matches it returns, and the caller raises its original 404 — so a genuinely
 * missing page stays a real 404 rather than a soft one.
 *
 * The Nuxt-context pieces are passed in rather than resolved here: this runs after the page's
 * `useAsyncData` await, past which composables would otherwise lose the Nuxt instance (E1001). The
 * `navigateTo` is wrapped in `runWithContext` for the same reason.
 */
export async function honorRedirect(lookupPath: string, ctx: RedirectContext): Promise<void> {
  const target = await ctx.client.resolveRedirect(lookupPath).catch(() => null);
  if (!target?.toPath) return;

  await ctx.nuxtApp.runWithContext(() =>
    navigateTo(ctx.localePath(target.toPath), {
      redirectCode: target.statusCode || 301,
      external: false,
    }),
  );
}
