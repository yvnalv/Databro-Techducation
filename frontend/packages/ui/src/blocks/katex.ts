import katex from "katex";

/**
 * Renders LaTeX to HTML with KaTeX.
 *
 * This is the **only** place the block renderer produces an HTML string, and it is a deliberate,
 * narrow exception to the "never v-html author-supplied content" rule that governs every other
 * renderer:
 *
 * * The input is LaTeX, not HTML. KaTeX parses it and emits its own markup — author text is never
 *   passed through as markup.
 * * `trust: false` (KaTeX's default, set explicitly here so it cannot drift) disables the commands
 *   that can emit raw HTML or arbitrary URLs, notably `\htmlClass`, `\includegraphics` and `\href`.
 * * `throwOnError: false` renders a malformed expression as visible error text instead of throwing,
 *   so one bad formula cannot fail the whole server render.
 *
 * If the KaTeX options ever need to change, re-check the first two points before doing it.
 */
export function renderMath(latex: string, displayMode: boolean): string {
  return katex.renderToString(latex, {
    displayMode,
    trust: false,
    strict: false,
    throwOnError: false,
    output: "html",
  });
}
