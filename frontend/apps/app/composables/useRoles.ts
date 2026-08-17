/**
 * Coarse role checks for navigation and landing (ADR-0015).
 *
 * **Affordances only, never a security boundary.** The API authorises every request independently
 * (docs/SECURITY.md §2), so the worst a wrong answer here can do is show someone a link to a page
 * whose requests will fail — not leak anything. Branch on this for what to *offer*; never for what
 * to *permit*.
 *
 * Roles rather than permissions on purpose: the JWT carries permissions and the profile carries
 * roles, and "can this person see the Studio link" is a question about the shape of their job, not
 * about a specific grant.
 */
const AUTHORING_ROLES = ["Author", "Editor", "Admin"];

export function useRoles() {
  const { user } = useAuth();

  const roles = computed(() => user.value?.roles ?? []);

  /** True for anyone with a reason to open the CMS at all. */
  const canAuthor = computed(() => roles.value.some((r) => AUTHORING_ROLES.includes(r)));

  /**
   * Where this user should land. Editors live in the Studio; everyone else is a learner, and
   * learners are the overwhelming majority, which is why they get the root.
   */
  const homePath = computed(() => (canAuthor.value ? "/studio" : "/"));

  return { roles, canAuthor, homePath };
}
