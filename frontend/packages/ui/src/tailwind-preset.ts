// Single source of design tokens for both DataBro apps (see docs/FRONTEND_ARCHITECTURE.md).
// Both apps consume this Tailwind preset so the design language never drifts between them.
//
// Two layers, deliberately:
//
//   1. `tokens` — raw values (fonts, the type scale). Swapping the typeface is an edit to this
//      object and nothing else.
//   2. Semantic colours (`surface`, `ink`, `border`, `accent`) resolved through CSS custom
//      properties. Components reference *meaning* — `text-ink-muted`, not `text-slate-500` — so
//      light and dark themes come from one set of class names, and a palette change does not
//      require touching a single component.
//
// The custom properties themselves live in `src/styles/tokens.css`, which also carries the rule
// that shapes this whole file: **an accent is a fill, never a text colour** (ADR-0020). Hence the
// `-on` / `-strong` pairs below. A plain `text-accent` deliberately does not exist.

export const tokens = {
  fontFamily: {
    // Poppins across display and body (ADR-0020 §3). Geometric, generous x-height — which is why
    // the line heights below run looser than Tailwind's defaults rather than tighter.
    display: ["Poppins", "Segoe UI", "system-ui", "sans-serif"],
    sans: ["Poppins", "Segoe UI", "ui-sans-serif", "system-ui", "sans-serif"],
    // Unambiguous 0/O and 1/l/I matter more here than on most platforms: this is a coding-education
    // product and the code blocks are the content.
    mono: ["JetBrains Mono Variable", "JetBrains Mono", "ui-monospace", "SFMono-Regular", "monospace"],
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
        // Surfaces. `inverse` is the emphasis surface — black cards and the rail in light mode,
        // cream blocks in dark. It replaces the gradient band as the way a section shouts.
        surface: {
          DEFAULT: withAlpha("--db-surface"),
          raised: withAlpha("--db-surface-raised"),
          sunken: withAlpha("--db-surface-sunken"),
          inverse: withAlpha("--db-surface-inverse"),
        },
        ink: {
          DEFAULT: withAlpha("--db-ink"),
          muted: withAlpha("--db-ink-muted"),
          subtle: withAlpha("--db-ink-subtle"),
          inverted: withAlpha("--db-ink-inverted"),
          // Pairs with `accent-deep`, which is black in both themes — so unlike `inverted` it does
          // not flip. Using `ink-inverted` on a deep band would go dark-on-black in dark mode.
          "on-deep": withAlpha("--db-ink-on-deep"),
        },
        line: {
          DEFAULT: withAlpha("--db-border"),
          strong: withAlpha("--db-border-strong"),
        },

        // Accent. `bg-accent` + `text-accent-on` for fills; `text-accent-strong` for type, borders
        // and focus rings. There is deliberately no plain `text-accent`.
        accent: {
          DEFAULT: withAlpha("--db-accent"),
          hover: withAlpha("--db-accent-hover"),
          on: withAlpha("--db-accent-on"),
          strong: withAlpha("--db-accent-strong"),
          subtle: withAlpha("--db-accent-subtle"),
          deep: withAlpha("--db-accent-deep"),
        },

        // Secondary is lime, and lime is a dark-surface colour. On a light ground it is only ever a
        // filled chip with `text-secondary-on`; `secondary-strong` covers type.
        secondary: {
          DEFAULT: withAlpha("--db-secondary"),
          on: withAlpha("--db-secondary-on"),
          strong: withAlpha("--db-secondary-strong"),
          subtle: withAlpha("--db-secondary-subtle"),
        },

        // Functional. Each has a `subtle` fill so a status can be tinted without inventing a colour.
        success: {
          DEFAULT: withAlpha("--db-success"),
          subtle: withAlpha("--db-success-subtle"),
        },
        warning: {
          DEFAULT: withAlpha("--db-warning"),
          subtle: withAlpha("--db-warning-subtle"),
        },
        danger: {
          DEFAULT: withAlpha("--db-danger"),
          subtle: withAlpha("--db-danger-subtle"),
        },
        info: {
          DEFAULT: withAlpha("--db-info"),
          subtle: withAlpha("--db-info-subtle"),
        },
        premium: {
          DEFAULT: withAlpha("--db-premium"),
          on: withAlpha("--db-premium-on"),
          strong: withAlpha("--db-premium-strong"),
          subtle: withAlpha("--db-premium-subtle"),
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
        display: [...tokens.fontFamily.display],
        sans: [...tokens.fontFamily.sans],
        mono: [...tokens.fontFamily.mono],
      },

      fontSize,

      maxWidth: {
        // ~68 characters. The single most load-bearing number for long-form readability: much wider
        // and the eye loses the line start on the return sweep.
        //
        // There is no `shell` here on purpose. The site shell width lives once, in `.db-shell`.
        prose: "68ch",
        // The cap on the app's content column, beside the rail. The frame itself is full-bleed
        // (`.db-app-shell`) so the rail sits against the window edge; this is what stops a card grid
        // stretching to absurd widths on an ultrawide display.
        app: "1760px",
      },

      // A 12/16/24 family (ADR-0020 §5). `control` exists because 60 call sites were reaching for
      // Tailwind's own 6px `rounded-md` default and so silently ignored the radius token entirely.
      borderRadius: {
        control: "0.75rem", // 12px — buttons, inputs, chips, menu items
        card: "1rem", // 16px — cards, tables, media
        panel: "1.5rem", // 24px — the rail, modals, page frames
      },

      // Soft and diffuse: a wide, low-opacity spread rather than a tight dark drop. On a cream
      // ground a hard shadow reads as dirt.
      boxShadow: {
        card: "0 1px 2px 0 rgb(18 18 18 / 0.04), 0 8px 24px -8px rgb(18 18 18 / 0.08)",
        lift: "0 2px 6px 0 rgb(18 18 18 / 0.06), 0 18px 40px -12px rgb(18 18 18 / 0.14)",
        panel: "0 4px 12px 0 rgb(18 18 18 / 0.08), 0 32px 64px -24px rgb(18 18 18 / 0.22)",
      },
    },
  },
};

export default preset;
