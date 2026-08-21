/**
 * The light/dark theme, persisted in the `databro_theme` cookie (ADR-0020 §6).
 *
 * Shared with the other app by cookie, exactly like `databro_locale`: a learner who switches to
 * dark on the catalogue should not land in a light dashboard one click later.
 *
 * **The theme is deliberately NOT rendered into the SSR markup.** `site` serves ISR-cached HTML, so
 * a `data-theme` baked in at render time would be one visitor's choice handed to everyone who hit
 * the cache afterwards. Instead a tiny script in `<head>` stamps the attribute from the cookie
 * before first paint (see `nuxt.config.ts`), and this composable owns it from hydration onward.
 * That also removes the flash of the wrong theme, which the SSR approach would still have had on
 * any cached page.
 *
 * Following `prefers-color-scheme` automatically stays deliberately unwired. The previous revision
 * of this system did that and dark-OS visitors saw a dark site the design never intended; the
 * switch is the opt-in, and the default is light.
 */
export type Theme = "light" | "dark";

export const THEME_COOKIE = "databro_theme";

export function useTheme() {
  const cookie = useCookie<Theme>(THEME_COOKIE, {
    default: () => "light",
    sameSite: "lax",
    path: "/",
    // A year: a theme preference going stale is worse than the cookie lingering.
    maxAge: 60 * 60 * 24 * 365,
  });

  const theme = computed<Theme>({
    get: () => (cookie.value === "dark" ? "dark" : "light"),
    set: (value) => {
      cookie.value = value;
      apply(value);
    },
  });

  const apply = (value: Theme) => {
    if (import.meta.client) document.documentElement.setAttribute("data-theme", value);
  };

  // The head script already stamped the attribute before paint. This re-asserts it after hydration
  // so a value changed in another tab — or restored from bfcache — cannot leave the DOM disagreeing
  // with the cookie.
  onMounted(() => apply(theme.value));

  const toggle = () => {
    theme.value = theme.value === "dark" ? "light" : "dark";
  };

  return { theme, toggle };
}
