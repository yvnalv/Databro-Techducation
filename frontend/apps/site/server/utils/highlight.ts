// The dependency-free subpath, never the package root: importing `@databro/ui` here pulls every
// `.vue` component into the Nitro server bundle, which Rollup cannot parse and which fails the
// build outright.
import { codeKey } from "@databro/ui/code-key";
import type { ContentBlock, ContentDocument } from "@databro/types";
import { createHighlighter, type Highlighter } from "shiki";

/**
 * Server-side syntax highlighting for code blocks.
 *
 * Runs only here, never in the browser. Shiki carries TextMate grammars for every language it
 * supports, and shipping that to a reader so they can look at a page we already rendered would be
 * several hundred kilobytes for zero benefit. The result travels in the page payload and the
 * renderer does a map lookup (see `codeHighlighterFor`).
 */

/**
 * Curated grammar set. Every grammar is loaded at startup, so this list is a real cost — it is the
 * languages DataBro actually teaches plus the ones its examples use, not everything Shiki supports.
 * Anything outside it renders as plain text, which is still readable code.
 */
const LANGUAGES = [
  "python",
  "sql",
  "javascript",
  "typescript",
  "jsx",
  "tsx",
  "bash",
  "json",
  "yaml",
  "csharp",
  "html",
  "css",
  "vue",
  "markdown",
  "dockerfile",
  "go",
  "rust",
  "r",
  "java",
] as const;

/** Light-only site (docs/DESIGN_SYSTEM.md), so one theme rather than a light/dark pair. */
const THEME = "github-light";

const SUPPORTED = new Set<string>(LANGUAGES);

/**
 * Created once per process, not per request. Building a highlighter compiles every grammar above;
 * doing that per request would put tens of milliseconds on the hottest cached path.
 *
 * Held as the in-flight promise rather than the resolved value so two concurrent first requests
 * share one initialisation instead of racing to build two.
 */
let highlighterPromise: Promise<Highlighter> | null = null;

function highlighter(): Promise<Highlighter> {
  highlighterPromise ??= createHighlighter({
    themes: [THEME],
    langs: [...LANGUAGES],
  });

  return highlighterPromise;
}

/** Normalises an author's language label to a grammar we actually loaded. */
function grammarFor(language: string | undefined): string | null {
  const normalized = language?.trim().toLowerCase();
  if (!normalized) return null;

  // Common aliases an author will reasonably type.
  const alias: Record<string, string> = {
    js: "javascript",
    ts: "typescript",
    py: "python",
    sh: "bash",
    shell: "bash",
    zsh: "bash",
    yml: "yaml",
    "c#": "csharp",
    cs: "csharp",
    golang: "go",
    postgres: "sql",
    postgresql: "sql",
    md: "markdown",
  };

  const resolved = alias[normalized] ?? normalized;
  return SUPPORTED.has(resolved) ? resolved : null;
}

/**
 * Highlights every code block in a document, returning a map keyed by {@link codeKey}.
 *
 * Walks nested blocks too: a list item may contain a code sample (ADR-0009), and a tutorial step
 * with a snippet in it is an ordinary thing to write.
 */
export async function highlightDocument(
  document: ContentDocument | undefined | null,
): Promise<Record<string, string>> {
  if (!document?.blocks?.length) return {};

  const samples = new Map<string, { code: string; grammar: string }>();
  collect(document.blocks, samples, 0);

  if (samples.size === 0) return {};

  const shiki = await highlighter();
  const highlighted: Record<string, string> = {};

  for (const [key, { code, grammar }] of samples) {
    try {
      highlighted[key] = shiki.codeToHtml(code, {
        lang: grammar,
        theme: THEME,
        // The renderer supplies the outer <pre>; Shiki emitting its own would nest two.
        structure: "inline",
      });
    } catch {
      // A grammar that fails on a specific sample must not take down the page. Omitting the key
      // makes the block fall back to plain text, which is exactly the right degradation.
    }
  }

  return highlighted;
}

/** Matches the renderer's nesting cap so a malformed document cannot make this recurse forever. */
const MAX_DEPTH = 2;

function collect(
  blocks: readonly ContentBlock[],
  into: Map<string, { code: string; grammar: string }>,
  depth: number,
): void {
  if (depth > MAX_DEPTH) return;

  for (const block of blocks) {
    if (block.type === "code") {
      const data = block.data as { code?: string; language?: string };
      const grammar = grammarFor(data?.language);

      if (data?.code && grammar) {
        into.set(codeKey(data.code, data.language ?? ""), { code: data.code, grammar });
      }
      continue;
    }

    if (block.type === "list") {
      const data = block.data as { items?: Array<{ blocks?: ContentBlock[] } | string> };
      for (const item of data?.items ?? []) {
        if (typeof item !== "string" && item?.blocks) collect(item.blocks, into, depth + 1);
      }
    }
  }
}
