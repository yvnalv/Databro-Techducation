import js from "@eslint/js";
import tseslint from "typescript-eslint";
import vue from "eslint-plugin-vue";
import vueParser from "vue-eslint-parser";

/**
 * One flat config for the whole workspace (docs/CODING_STANDARDS.md).
 *
 * Deliberately **not** type-aware linting: it would need a TS program per package, roughly triples
 * the run time, and the rules it unlocks are largely ones the compiler already enforces here —
 * `pnpm typecheck` runs `vue-tsc`/`tsc` across every workspace and is the real correctness gate.
 * This catches the class of mistake a type checker cannot: unused code, accidental globals,
 * template accessibility, and Vue conventions.
 *
 * Rules are added when something real goes wrong, not adopted wholesale. A config that fires on
 * hundreds of pre-existing lines gets suppressed rather than fixed, and then it protects nothing.
 */
export default tseslint.config(
  {
    // Generated, vendored, or build output. `.nuxt` and `.output` in particular contain thousands
    // of generated files that would otherwise dominate every run.
    ignores: [
      "**/node_modules/**",
      "**/.nuxt/**",
      "**/.output/**",
      "**/dist/**",
      "**/coverage/**",
    ],
  },

  js.configs.recommended,
  ...tseslint.configs.recommended,
  ...vue.configs["flat/recommended"],

  {
    files: ["**/*.{ts,vue}"],
    languageOptions: {
      parser: vueParser,
      parserOptions: {
        // The Vue parser handles <template>; TypeScript inside <script setup lang="ts"> is handed
        // to the TS parser. Without this, every typed SFC is a parse error.
        parser: tseslint.parser,
        ecmaVersion: "latest",
        sourceType: "module",
        extraFileExtensions: [".vue"],
      },
    },
    rules: {
      // Nuxt auto-imports (`ref`, `computed`, `useState`, …) are globals the linter cannot see, and
      // teaching it every one is a list that rots. The type checker already catches a genuinely
      // undefined identifier, so this rule would only produce false positives.
      "no-undef": "off",

      // A leading underscore is the conventional "deliberately unused" marker — a destructured
      // element being skipped, or a signature that must match an interface.
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_", caughtErrors: "none" },
      ],

      // `any` appears in the block editor where an untyped JSONB document meets typed components.
      // That boundary is real and documented; warn so it stays visible without blocking a commit.
      "@typescript-eslint/no-explicit-any": "warn",

      // Single-word component names are fine in an app: `pages/index.vue` and `layouts/default.vue`
      // are Nuxt conventions, not naming mistakes.
      "vue/multi-word-component-names": "off",

      // Written for Options-API prop objects. With a TypeScript `subtitle?: string`, `undefined`
      // *is* the meaningful "not provided" state and the template branches on it — inventing a
      // default would replace a deliberate absence with an empty string.
      "vue/require-default-prop": "off",

      // Escalated from the recommended config's `warn`. This renderer's whole security posture is
      // that authored content becomes elements, never markup (docs/SECURITY.md §5) — a warning that
      // still lets CI pass is not a guard.
      //
      // The sanctioned uses (KaTeX and Shiki, both rendering strings we generate ourselves from
      // plain text) carry an `eslint-disable-next-line` with a reason at the line itself, rather
      // than being exempted here by filename. A justification belongs next to the code it excuses,
      // where a reviewer will actually read it.
      "vue/no-v-html": "error",

      // Formatting is not this tool's job — these fight a formatter and produce noise.
      "vue/max-attributes-per-line": "off",
      "vue/singleline-html-element-content-newline": "off",
      "vue/multiline-html-element-content-newline": "off",
      "vue/html-self-closing": "off",
      "vue/html-indent": "off",
      "vue/html-closing-bracket-newline": "off",
      "vue/attributes-order": "off",
    },
  },

  {
    files: ["**/*.spec.ts"],
    rules: {
      // Tests deliberately build malformed inputs to prove the renderer degrades rather than throws.
      "@typescript-eslint/no-explicit-any": "off",
    },
  },
);
