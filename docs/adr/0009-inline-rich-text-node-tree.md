# ADR-0009 — Inline rich text as a ProseMirror-compatible node tree

Status: Accepted
Date: 2026-07-30
Deciders: Project owner

## Context

Every text field in the Phase 1 block catalogue is a plain string: `paragraph.text`,
`list.items[]`, `table.rows[][]`, `callout.text`. The consequence is that **a published article
cannot contain a hyperlink**, inline code, or emphasis — no citations, no linking to external docs,
no internal linking beyond taxonomy. For an education-first platform whose acquisition strategy is
long-form technical content, that is the single most limiting property of the model.

`ParagraphBlock` already declares `marks?: unknown[]`, so range-based marks were the original
intent, but the shape was never specified or implemented (ADR-0004 deferred it).

The decision cannot be deferred past the CMS editor: the editor is built around whatever shape the
inline content has, and rewriting it afterwards means rewriting the editor.

## Decision

Store inline content as an **array of inline nodes shaped like ProseMirror's document model** — the
model Tiptap (the intended Vue 3 editor) uses natively.

```jsonc
{ "type": "paragraph", "data": { "content": [
  { "type": "text", "text": "Install it with " },
  { "type": "text", "text": "pip install databro", "marks": [{ "type": "code" }] },
  { "type": "text", "text": " — see the " },
  { "type": "text", "text": "docs", "marks": [{ "type": "link", "attrs": { "href": "https://…" } }] }
]}}
```

* An inline node is either a `text` node with optional marks, or an atomic inline node
  (`mathInline`).
* Marks are `bold`, `italic`, `code`, `strike`, and `link` (with `href`).
* The block fields carrying inline content are `paragraph`, `callout`, `quote`, list items, and
  table cells. **`heading` stays a plain string** — links and emphasis inside headings hurt both the
  document outline and anchor generation.

Two structural changes land with it, because both are expensive to retrofit and cheap now:

* **List items may contain blocks.** An item is `{ content: InlineNode[], blocks?: ContentBlock[] }`,
  so a tutorial step can hold a code sample. Nesting is depth-capped at render time so a malformed
  document cannot exhaust the stack during SSR.
* **`math` becomes a Phase 1 block type** (plus `mathInline`), moved forward from "reserved for later
  phases". Explaining attention, gradients and loss functions is core Phase 1 subject matter here,
  not a Phase 2 nicety.

## Alternatives considered

* **Offset-range marks** (`{ text, marks: [{ type, from, to }] }`) — compact and simple to render,
  and what the existing type hint implied. Rejected: every insertion shifts every subsequent offset,
  so the editor must continuously remap ranges, and ranges do not nest. It would also mean a
  permanent translation layer between storage and the editor's own model — the exact cost this
  decision exists to avoid.
* **Markdown strings per field** — familiar and compact, but reintroduces a parser on the render
  path, makes per-block analytics and structured editing harder, and contradicts ADR-0004's reason
  for choosing typed blocks over Markdown in the first place.
* **Links only, no general rich text** — the narrowest fix. Rejected because it settles nothing: the
  model question returns the first time emphasis or inline code is wanted, and by then the editor
  exists.

## Consequences

* Positive: the storage model and the editor model are the same shape, so the CMS needs no
  translation layer; inline content nests, so list items and table cells get rich text for free.
* Positive: `paragraph.marks` stops being a reserved-but-unspecified field, which was a standing
  ambiguity in the content contract.
* Trade-offs: JSONB documents are more verbose than plain strings, and rendering inline content is a
  recursive walk rather than an interpolation. Both are acceptable on a cached, read-heavy path.
* Trade-offs: this is a **breaking change to the block contract**. There is no production content, so
  no data migration is written; instead the renderers accept a legacy `text: string` in place of
  `content`, so existing local/seeded documents keep rendering. That fallback is a compatibility
  shim, not a supported authoring shape.
* Obligates: **text is still never rendered as HTML.** Marks map to elements (`<strong>`, `<code>`,
  `<a>`), and link `href` values are scheme-checked exactly like embed URLs. The single deliberate
  exception is KaTeX output, which is generated from LaTeX by a trusted library with HTML injection
  disabled — documented at the call site.
* Obligates: Phase 2 Lessons inherit this model unchanged (ADR-0007), so it must stay
  context-agnostic.

## References

[CONTENT_MODEL.md](../CONTENT_MODEL.md); ADR-0004 (typed blocks as JSONB); ADR-0007 (one content
engine); [SECURITY.md](../SECURITY.md).
