/**
 * Route guard for the authoring app.
 *
 * Global rather than per-page: the CMS is authenticated by default, so the safe posture is
 * "everything is protected unless listed", not "protected if someone remembered to add middleware".
 * Forgetting a `definePageMeta` should not expose a page.
 *
 * This is a **UX** guard, not a security boundary — the API enforces permissions on every request
 * (docs/SECURITY.md §2). Its job is to send someone to the login screen instead of showing them a
 * dashboard that will only 401.
 */
const PUBLIC_ROUTES = new Set(["/login"]);

export default defineNuxtRouteMiddleware(async (to) => {
  if (PUBLIC_ROUTES.has(to.path)) return;

  const { isAuthenticated, ensureUser } = useAuth();

  if (!isAuthenticated.value) {
    // Carry the intended destination so login can return there rather than dumping everyone on the
    // dashboard.
    return navigateTo({ path: "/login", query: { redirect: to.fullPath } });
  }

  // A cookie can outlive its session (revoked refresh token, restarted API). Probing here means an
  // expired session lands on the login screen instead of a page full of failed requests.
  if (!(await ensureUser())) {
    return navigateTo({ path: "/login", query: { redirect: to.fullPath } });
  }
});
