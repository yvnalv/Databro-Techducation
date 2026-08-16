import { ApiClientError, createApiClient, type ApiClient } from "@databro/api-client";
import type { UserProfile } from "@databro/types";

/**
 * Session for the authoring app.
 *
 * **Token storage.** Tokens live in cookies rather than `localStorage`, because SSR needs to read
 * them: on the server there is no `localStorage`, and without a cookie every authenticated page
 * would have to render empty and re-fetch on the client.
 *
 * They are **not `httpOnly`** — the app sets them from JS, so it cannot be. That is the standard
 * SPA trade-off and it means an XSS would expose the token. Mitigations in place: `sameSite: strict`
 * (blocks cross-site sends), `secure` outside development, a short access-token lifetime, and the
 * renderer's absolute refusal to inject author content as HTML. The proper hardening is a
 * backend-for-frontend that proxies login and sets `httpOnly` cookies the browser never reads;
 * that is a deliberate follow-up, recorded in STATUS.md, not an oversight.
 */
const ACCESS_COOKIE = "databro_at";
const REFRESH_COOKIE = "databro_rt";

export function useAuth() {
  const config = useRuntimeConfig();

  const accessToken = useCookie<string | null>(ACCESS_COOKIE, {
    sameSite: "strict",
    secure: !import.meta.dev,
    // Session cookie: closing the browser ends the session. The refresh token carries longevity.
    path: "/",
  });

  const refreshToken = useCookie<string | null>(REFRESH_COOKIE, {
    sameSite: "strict",
    secure: !import.meta.dev,
    path: "/",
    maxAge: 60 * 60 * 24 * 14, // matches the API's RefreshTokenDays
  });

  // Shared across composable calls within a request/page, so `me()` is fetched once.
  const user = useState<UserProfile | null>("auth:user", () => null);

  const isAuthenticated = computed(() => Boolean(accessToken.value));

  function baseUrl() {
    return (
      (import.meta.server && config.apiInternalBaseUrl) || config.public.apiBaseUrl
    ) as string;
  }

  /** A client that sends the current access token. */
  function client(): ApiClient {
    return createApiClient({ baseUrl: baseUrl(), getToken: () => accessToken.value });
  }

  /** A client with no token — for login and refresh, which must work without one. */
  function anonymousClient(): ApiClient {
    return createApiClient({ baseUrl: baseUrl() });
  }

  function setSession(tokens: { accessToken: string; refreshToken: string }) {
    accessToken.value = tokens.accessToken;
    refreshToken.value = tokens.refreshToken;
  }

  function clearSession() {
    accessToken.value = null;
    refreshToken.value = null;
    user.value = null;
  }

  async function login(email: string, password: string) {
    const tokens = await anonymousClient().login(email, password);
    setSession(tokens);
    user.value = await client().me();
  }

  async function logout() {
    clearSession();
    await navigateTo("/login");
  }

  /**
   * Exchanges the refresh token for a new pair. Returns false when there is nothing to refresh or
   * the token has been revoked — the caller then treats the session as over.
   *
   * The API rotates refresh tokens and invalidates the chain on reuse (docs/SECURITY.md §1), so a
   * failed refresh must clear the session rather than be retried.
   */
  async function refresh(): Promise<boolean> {
    if (!refreshToken.value) return false;

    try {
      setSession(await anonymousClient().refresh(refreshToken.value));
      return true;
    } catch {
      clearSession();
      return false;
    }
  }

  /**
   * Runs an authenticated call, refreshing once on a 401 and retrying.
   *
   * Only 401 triggers a refresh: a 403 means the token is valid but the role is insufficient, and
   * refreshing would loop without ever fixing it.
   */
  async function withAuth<T>(call: (api: ApiClient) => Promise<T>): Promise<T> {
    try {
      return await call(client());
    } catch (error) {
      const unauthorized = error instanceof ApiClientError && error.status === 401;
      if (!unauthorized) throw error;

      if (!(await refresh())) throw error;
      return await call(client());
    }
  }

  /** Loads the profile if a token exists but the user has not been fetched this navigation. */
  async function ensureUser(): Promise<UserProfile | null> {
    if (!accessToken.value) return null;
    if (user.value) return user.value;

    try {
      user.value = await withAuth((api) => api.me());
    } catch {
      clearSession();
    }
    return user.value;
  }

  return {
    user,
    isAuthenticated,
    login,
    logout,
    refresh,
    withAuth,
    ensureUser,
  };
}
