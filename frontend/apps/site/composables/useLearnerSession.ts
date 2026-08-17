import { ApiClientError, createApiClient, type ApiClient } from "@databro/api-client";

/**
 * A **read-only** session for the public site (ADR-0005, ADR-0015).
 *
 * This is the "auth-aware, not auth-only" half of ADR-0005 that had never been built: `site` renders
 * public content for everyone, and additionally recognises a signed-in learner so a lesson page can
 * offer progress controls.
 *
 * <b>The site cannot sign anyone in.</b> It reads the cookies `app` sets and nothing more — no login
 * form, no refresh-token rotation, no session state of its own. Duplicating login here would mean
 * two implementations of the most security-sensitive flow we have, and the second would be the one
 * nobody remembers to patch. {@link signInUrl} sends people to the app for that.
 *
 * <b>How the cookie is shared.</b> Cookies are scoped by host and path, never by port, so in local
 * development `localhost:3000` and `localhost:3001` genuinely share them. In production the two apps
 * are separate subdomains and the cookie needs an explicit parent `domain` — recorded in STATUS as
 * owed with the first real deploy, because it cannot be verified from here.
 */
const ACCESS_COOKIE = "databro_at";
const REFRESH_COOKIE = "databro_rt";

export function useLearnerSession() {
  const config = useRuntimeConfig();

  // `readonly` in intent: the site never writes these. Nuxt has no read-only cookie helper, so the
  // discipline is the absence of an assignment anywhere in this file.
  const accessToken = useCookie<string | null>(ACCESS_COOKIE, { sameSite: "strict", path: "/" });
  const refreshToken = useCookie<string | null>(REFRESH_COOKIE, { sameSite: "strict", path: "/" });

  const isSignedIn = computed(() => Boolean(accessToken.value));

  function client(): ApiClient {
    const baseUrl = ((import.meta.server && config.apiInternalBaseUrl) ||
      config.public.apiBaseUrl) as string;

    return createApiClient({ baseUrl, getToken: () => accessToken.value });
  }

  /**
   * Runs an authenticated call and reports whether the session has expired, rather than throwing.
   *
   * Progress is a **secondary** affordance on a page whose main job is rendering a lesson. A dead
   * token must degrade the controls to "sign in to track progress" — never take down the page the
   * reader came for.
   */
  async function tryAuthed<T>(
    call: (api: ApiClient) => Promise<T>,
  ): Promise<{ ok: true; value: T } | { ok: false; expired: boolean }> {
    if (!accessToken.value) return { ok: false, expired: false };

    try {
      return { ok: true, value: await call(client()) };
    } catch (error) {
      const status = error instanceof ApiClientError ? error.status : 0;

      // 401 means the access token is dead. The site deliberately does **not** refresh it: rotation
      // invalidates the chain on reuse (docs/SECURITY.md §1), and two apps racing to rotate the same
      // refresh token would revoke a perfectly good session. Only `app` rotates.
      if (status === 401) return { ok: false, expired: true };

      throw error;
    }
  }

  /** Where to send someone to sign in, returning them to this page afterwards. */
  function signInUrl(returnTo: string) {
    const appUrl = String(config.public.appUrl ?? "").replace(/\/$/, "");
    const absolute = `${String(config.public.siteUrl).replace(/\/$/, "")}${returnTo}`;

    // The app's login allowlists exactly this origin (read from its own config) and rejects every
    // other absolute URL, so the learner comes back to the lesson they were reading rather than
    // landing on their dashboard.
    return `${appUrl}/login?redirect=${encodeURIComponent(absolute)}`;
  }

  return { isSignedIn, accessToken, refreshToken, tryAuthed, signInUrl };
}
