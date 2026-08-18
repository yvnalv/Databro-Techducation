/**
 * Route guard for the authenticated app.
 *
 * Global rather than per-page: everything here is behind auth by default, so the safe posture is
 * "protected unless listed", not "protected if someone remembered to add middleware". Forgetting a
 * `definePageMeta` should not expose a page.
 *
 * This is a **UX** guard, not a security boundary — the API enforces permissions on every request
 * (docs/SECURITY.md §2). Its job is to send someone to the login screen instead of showing them a
 * dashboard that will only 401.
 */
// `/verify-email` is public for the same reason `/login` is: the token in the link is the proof,
// and a person arriving from their inbox has by definition not signed in yet.
// All public for the same reason: someone arriving here either cannot sign in yet or cannot sign in
// at all, and the token in the link is the proof.
const PUBLIC_ROUTES = new Set([
  "/login",
  "/verify-email",
  "/forgot-password",
  "/reset-password",
]);

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

  // A learner who reaches /studio — by typing it, or by an old bookmark from before ADR-0015 moved
  // the CMS — goes to their dashboard rather than to a shell whose every request will 403. Sending
  // them somewhere useful is the whole difference between a guard and an error page.
  //
  // Not a security boundary, and not treated as one: the API authorises independently, so this is
  // purely about not showing someone a room they have no use for.
  if (to.path === "/studio" || to.path.startsWith("/studio/")) {
    const { canAuthor } = useRoles();
    if (!canAuthor.value) return navigateTo("/", { replace: true });
  }
});
