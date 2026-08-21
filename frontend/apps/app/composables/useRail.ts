/**
 * Whether the navigation rail is collapsed to icons.
 *
 * Persisted in the `databro_rail` cookie, like the theme and the locale. A rail that springs back
 * open on every navigation is worse than one that never collapsed: the control would appear not to
 * work. The cookie also means the server renders the same width the client is about to, so the
 * layout does not jump on hydration.
 */
export const RAIL_COOKIE = "databro_rail";

export function useRail() {
  const cookie = useCookie<"open" | "collapsed">(RAIL_COOKIE, {
    default: () => "open",
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 365,
  });

  const collapsed = computed({
    get: () => cookie.value === "collapsed",
    set: (value) => {
      cookie.value = value ? "collapsed" : "open";
    },
  });

  const toggle = () => {
    collapsed.value = !collapsed.value;
  };

  return { collapsed, toggle };
}
