// Single source of design tokens for both DataBro apps (see docs/FRONTEND_ARCHITECTURE.md).
// Both apps consume this Tailwind preset so the design language never drifts between them.
//
// Two layers, deliberately:
//
//   1. `tokens` — raw values (the brand ramp, fonts, the type scale). Swapping the palette or the
//      typeface is an edit to this object and nothing else.
//   2. Semantic colours (`surface`, `ink`, `border`, `accent`) resolved through CSS custom
//      properties. Components reference *meaning* — `text-ink-muted`, not `text-slate-500` — so
//      light and dark themes come from one set of class names, and a palette change does not
//      require touching a single component.
//
// The custom properties themselves live in `src/styles/tokens.css`.

export const tokens = {
  colors: {
    brand: {
      50: "#eef6ff",
      100: "#d9ebff",
      200: "#bcdcff",
      300: "#8ec6ff",
      400: "#59a6ff",
      500: "#3385f6",
      600: "#1f66db",
      700: "#1a51b0",
      800: "#1b458c",
      900: "#1b3d73",
    },
  },
  fontFamily: {
    sans: ["Inter", "ui-sans-serif", "system-ui", "sans-serif"],
    mono: ["JetBrains Mono", "ui-monospace", "SFMono-Regular", "monospace"],
  },
} as const;

import type { Config } from "tailwindcss";

/**
 * Type scale on a ~1.25 (major third) ratio, with line heights tuned for long-form reading rather
 * than Tailwind's UI-oriented defaults: body copy is looser, display sizes are tighter.
 */
const fontSize: Record<string, [string, Record<string, string>]> = {
  xs: ["0.75rem", { lineHeight: "1.125rem" }],
  sm: ["0.875rem", { lineHeight: "1.375rem" }],
  base: ["1rem", { lineHeight: "1.75rem" }],
  lg: ["1.125rem", { lineHeight: "1.875rem" }],
  xl: ["1.25rem", { lineHeight: "1.875rem" }],
  "2xl": ["1.5rem", { lineHeight: "2rem" }],
  "3xl": ["1.875rem", { lineHeight: "2.375rem" }],
  "4xl": ["2.25rem", { lineHeight: "2.625rem", letterSpacing: "-0.02em" }],
  "5xl": ["3rem", { lineHeight: "3.25rem", letterSpacing: "-0.025em" }],
  "6xl": ["3.75rem", { lineHeight: "4rem", letterSpacing: "-0.03em" }],
};

/** `<alpha-value>` keeps Tailwind opacity modifiers (`text-ink/70`) working through a variable. */
const withAlpha = (variable: string) => `rgb(var(${variable}) / <alpha-value>)`;

// Typed at the source rather than cast at each point of use, so a malformed token is caught here.
const preset: Partial<Config> = {
  darkMode: ["class", '[data-theme="dark"]'],
  theme: {
    extend: {
      colors: {
        // Raw ramp, for the rare case a specific step is genuinely wanted.
        brand: { ...tokens.colors.brand },

        // Semantic surfaces and text. These are what components should use.
        surface: {
          DEFAULT: withAlpha("--db-surface"),
          raised: withAlpha("--db-surface-raised"),
          sunken: withAlpha("--db-surface-sunken"),
        },
        ink: {
          DEFAULT: withAlpha("--db-ink"),
          muted: withAlpha("--db-ink-muted"),
          subtle: withAlpha("--db-ink-subtle"),
          inverted: withAlpha("--db-ink-inverted"),
        },
        line: {
          DEFAULT: withAlpha("--db-border"),
          strong: withAlpha("--db-border-strong"),
        },
        accent: {
          DEFAULT: withAlpha("--db-accent"),
          hover: withAlpha("--db-accent-hover"),
          subtle: withAlpha("--db-accent-subtle"),
        },

        // Callout variants, so the four states are themeable rather than hardcoded per component.
        note: {
          info: withAlpha("--db-note-info"),
          tip: withAlpha("--db-note-tip"),
          warning: withAlpha("--db-note-warning"),
          danger: withAlpha("--db-note-danger"),
        },
      },

      fontFamily: {
        sans: [...tokens.fontFamily.sans],
        mono: [...tokens.fontFamily.mono],
      },

      fontSize,

      maxWidth: {
        // ~68 characters. The single most load-bearing number for long-form readability: much wider
        // and the eye loses the line start on the return sweep.
        prose: "68ch",
        // Wider container for listings and chrome, which are scanned rather than read.
        shell: "72rem",
      },

      borderRadius: {
        card: "0.75rem",
      },

      boxShadow: {
        card: "0 1px 2px 0 rgb(0 0 0 / 0.04), 0 4px 16px -4px rgb(0 0 0 / 0.08)",
        "card-hover": "0 2px 4px 0 rgb(0 0 0 / 0.06), 0 12px 28px -8px rgb(0 0 0 / 0.14)",
      },
    },
  },
};

export default preset;
