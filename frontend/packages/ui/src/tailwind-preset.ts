// Single source of design tokens for both DataBro apps (see docs/FRONTEND_ARCHITECTURE.md).
// Both apps consume this Tailwind preset so the design language never drifts between them.

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

// Typed loosely to avoid a hard dependency on tailwindcss types at the package level.
const preset = {
  theme: {
    extend: {
      colors: {
        brand: tokens.colors.brand,
      },
      fontFamily: tokens.fontFamily,
    },
  },
};

export default preset;
