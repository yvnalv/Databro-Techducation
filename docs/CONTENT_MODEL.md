# DataBro — Content Model

This is DataBro's core domain document. The content engine is built once and reused everywhere
(articles now, lessons later). See [ADR-0004](DECISIONS.md) and [ADR-0007](DECISIONS.md).

## 1. Core idea

A **Content Unit** is a renderable piece of learning material composed of an ordered list of typed
**Content Blocks**. Both an **Article** and (in Phase 2) a **Lesson** are Content Units — they differ
only in context:

* An **Article** is a standalone, SEO-oriented Content Unit.
* A **Lesson** is a Content Unit that belongs to a `CourseModule` and adds learning metadata
  (objectives, prerequisites, estimated time, difficulty, ordering, related lessons).

We never build a second content system. Lessons wrap the same blocks + versioning engine.

**In code, that engine is the abstract `ContentUnit` aggregate** (`Content.Domain`). It owns the
block pair (`DraftBlocks` / `PublishedBlocks`), the published snapshot that keeps drafts off public
surfaces (`PublishedTitle` / `PublishedSummary` / `SearchText`, CT-6), version history, and the
draft → scheduled → published state machine. `Article` is that engine plus what only a standalone
discoverable page has — byline, taxonomy, SEO metadata, locale. A lesson body will be the same
engine with none of those.

Each concrete type maps to **its own table** ([ADR-0012](adr/0012-lesson-bodies-live-in-content.md)),
so a lesson cannot be returned by a query over articles. Publishing is one implementation; only the
domain event raised at the end differs, via a hook each type overrides.

## 2. Blocks are typed JSON

Content is stored as a JSON array of blocks (persisted in `jsonb`). Each block has a stable `id`, a
`type`, and a `data` object whose shape depends on the type. This keeps authoring flexible, renders
cleanly in Nuxt, supports per-block features (analytics, quiz refs), and stays parseable for future AI
embeddings — without a heavyweight block-per-row schema.

```jsonc
{
  "version": 1,
  "blocks": [
    { "id": "b1", "type": "heading", "data": { "level": 2, "text": "Getting Started" } },
    { "id": "b2", "type": "paragraph", "data": { "text": "Python is …", "marks": [] } },
    { "id": "b3", "type": "code", "data": { "language": "python", "code": "print('hi')",
                                            "runnable": false } },
    { "id": "b4", "type": "callout", "data": { "variant": "tip", "text": "Use a venv." } },
    { "id": "b5", "type": "image", "data": { "mediaId": "…", "alt": "diagram", "caption": "" } }
  ]
}
```

## 3. Block type catalog (Phase 1)

| type | purpose | key data fields |
|---|---|---|
| `heading` | section heading | `level` (2–4), `text` — **plain string by design** |
| `paragraph` | prose | `content` (inline nodes) |
| `code` | code sample | `language`, `code`, `filename?`, `output?`, `runnable` (reserved for Playground) |
| `callout` | note/tip/warning | `variant` (info/tip/warning/danger), `content` |
| `image` | media | `mediaId`, `alt`, `caption?` |
| `quote` | blockquote | `content`, `attribution?` |
| `list` | ordered/unordered | `ordered`, `items[]` — each `{ content, blocks? }` |
| `divider` | separator | — |
| `embed` | external embed | `provider`, `url` (allowlisted providers only) |
| `table` | simple table | `headers[]`, `rows[][]` — cells carry inline content |
| `math` | block equation | `latex` (KaTeX) |

Reserved for later phases: `quiz-ref` (P2), `exercise` (P3), `interactive-playground` (P3),
`file-download`.

### 3a. Inline content (ADR-0009)

Text-bearing fields hold an **array of inline nodes**, shaped like ProseMirror's document model —
the model Tiptap uses natively, so the CMS editor needs no translation layer.

```jsonc
"content": [
  { "type": "text", "text": "Install with " },
  { "type": "text", "text": "pip install databro", "marks": [{ "type": "code" }] },
  { "type": "text", "text": " — see the " },
  { "type": "text", "text": "docs", "marks": [{ "type": "link", "attrs": { "href": "https://…" } }] },
  { "type": "mathInline", "attrs": { "latex": "O(n^2)" } }
]
```

* Marks: `bold`, `italic`, `code`, `strike`, `link` (`attrs.href`).
* Inline nodes: `text`, and the atomic `mathInline`.
* Inline content appears in `paragraph`, `callout`, `quote`, list items and table cells.
  **`heading` deliberately stays a plain string** — emphasis or links inside a heading hurt the
  document outline and anchor generation.
* **Legacy shim:** a plain `text: string` is still accepted wherever `content` is expected, so
  documents written before ADR-0009 keep rendering. It is a compatibility shim, not a supported
  authoring shape.

**The Phase 1 catalog above is implemented and closed.** All eleven types have renderers in
`packages/ui/src/blocks`, and the registry is typed `Record<BlockType, Component>` so adding a member
to `BlockType` fails the build until a renderer exists. One field remains reserved but unimplemented:
`code.runnable` (Playground, Phase 3).

### Block invariants

* Every block has a unique, stable `id` (survives edits — enables per-block analytics/anchors).
* `image`/media blocks reference Media by `mediaId` (no embedded URLs — Media owns the URL). Until
  Media exists, the renderer emits an accessible placeholder carrying the `alt` text rather than
  dropping the block.
* `embed` providers are allowlisted (security — no arbitrary iframes). The allowlist normalizes each
  URL into the provider's documented embed form, requires https, and degrades anything unrecognised
  to a `nofollow noopener` link. Framed content is sandboxed. See
  `packages/ui/src/blocks/embed-providers.ts`.
* **Block text is never rendered as HTML.** Block data is author-supplied and reaches the renderer
  straight out of JSONB, so interpolating it as markup would make the CMS a stored-XSS vector.
  Marks map to *elements* (`<strong>`, `<code>`, `<a>`), never to HTML strings, and a link `href`
  is scheme-checked exactly like an embed URL — a `javascript:` or `data:` href drops the anchor
  while keeping the prose.
* **One deliberate exception:** KaTeX output. Its input is LaTeX rather than HTML, it runs with
  `trust: false` so the commands that can emit markup or arbitrary URLs are disabled, and
  `throwOnError: false` renders a malformed formula as visible error text instead of failing the
  whole server render. The reasoning lives at the call site in `packages/ui/src/blocks/katex.ts`.
* **Nesting is depth-capped.** List items may contain blocks, which makes rendering recursive; past
  one level of nesting the nested blocks are dropped, so a malformed document cannot exhaust the
  stack during SSR.
* Unknown block types degrade gracefully (forward compatibility): content outlives renderers, so a
  published document may carry a type added after the current bundle shipped. The public site hides
  them; the CMS preview shows a placeholder (`ContentRenderer` prop `showUnknownBlocks`).

## 4. Versioning & publishing

A Content Unit maintains two representations:

* **`draft_blocks`** — the mutable working copy edited in the CMS.
* **`published_blocks`** — an immutable snapshot served to the public; only present after first publish.

Lifecycle (`status`): `draft → scheduled → published ⇄ unpublished → archived`.

Transitions:

* **Save draft:** mutate `draft_blocks`. No public effect.
* **Publish:** validate → snapshot `draft_blocks` into `published_blocks` → write an
  `article_versions` row → increment `current_version` → set `published_at` → emit `ArticlePublished`
  (outbox). Public reads switch to the new snapshot; caches invalidate; search reindexes.
* **Schedule:** set `scheduled_for`; a Hangfire job publishes at that time.
* **Unpublish:** hide from public; keep versions. Emit `ArticleUnpublished`.
* **Archive:** soft-remove from listings; retains history.

Rules:

* Public consumers **only** ever see `published_blocks`. Editing a published article changes the draft
  until re-published.
* `article_versions` is append-only and immutable — full audit of what was published when.

## 5. Identity & SEO fields (per Content Unit)

* `slug` — unique, URL-safe. **Immutable once published.** Changing it creates a `redirects` (301)
  record. See [SEO.md](SEO.md).
* `title`, `summary` — used for listings, cards, meta.
* `seo` (jsonb) — meta title/description, canonical override, robots directives, OG image ref.
* `visibility` — `public` | `premium`. Reserved from P1; billing (P3) activates gating. Premium units
  still expose SEO metadata + a preview publicly (see [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)).
* `reading_time_minutes` — derived from block content on save.

## 5b. Taxonomy

Categories and tags are **separate aggregates** from the content unit. An article references them by
id only — no navigation property — so the Article aggregate boundary stays intact and Lessons can
reuse the same arrangement in Phase 2.

| | Shape | Cardinality | Rules |
|---|---|---|---|
| **Category** | Hierarchical (`parent_id`, `order`) | At most one per article (CT-11) | TX-1 slug unique among categories; TX-2 cannot be deleted while referenced; TX-3 no cycles |
| **Tag** | Flat | Any number per article (CT-11) | TX-1 slug unique among tags |

Notes that matter in practice:

* Uniqueness is **per type**, so `/categories/python` and `/tags/python` are different pages by
  design.
* Category and tag slugs are **immutable**, exactly like article slugs (CT-2/CT-3): they are public
  URLs, so renaming one needs a 301 record. Only the display name is editable. Slug changes arrive
  with the redirects work.
* Deleting a tag is always allowed; the soft-delete filter removes it from every article's tag list,
  so a deleted tag cannot surface on a public page. Deleting a *category* is refused while articles
  or child categories still reference it (TX-2).
* Creating a term requires `Taxonomy.Manage` (Editor/Admin). An **Author** may assign existing terms
  while editing their article but cannot mint new ones — the split that keeps tag vocabulary from
  sprawling.

## 6. Localization of content

* Article **bodies** are authored per-locale as separate Content Units linked by `translation_group_id`.
  This is distinct from UI-chrome i18n (see [CLAUDE.md](../CLAUDE.md) → Internationalization).
* A locale variant can exist without others; the reader is offered available translations.

## 7. Rendering contract

* The `site` app receives `published_blocks` as JSON and renders each block via a registered Vue
  component keyed by `type`.
* Renderers must be pure/deterministic and safe: text is interpolated (never `v-html`), and only
  allowlisted embed providers render.
* The same renderer package (`packages/ui`) is used by the CMS preview in `app` — one renderer, no
  drift between preview and production.

### Implementation

`@databro/ui` exports `ContentRenderer` (takes a `ContentDocument`) and the `blockRegistry` it
resolves against.

| Prop | Purpose |
|---|---|
| `document` | The `ContentDocument` to render. |
| `showUnknownBlocks` | `false` on `site`, `true` for CMS preview. |
| `resolveMediaUrl` | Resolves an `ImageBlock.mediaId` to a URL. Supplied when Media lands; until then images render a placeholder. |

`resolveMediaUrl` and the renderer options are supplied via provide/inject rather than prop-drilling,
so the eight block components that need neither stay decoupled from both.

Two integration notes that are easy to get wrong:

* Block headings render as `h2`–`h4` (clamped) with slugified anchor ids. The article title owns the
  page's only `h1`, so the document outline stays well-formed.
* An app consuming the renderer must add `packages/ui/src` to its Tailwind `content` globs. The
  package sits outside the app root, so without it the block styles are purged from the production
  build.

## 8. Why not Markdown/MDX or normalized rows

* **Markdown/MDX string:** simplest, but boxes us out of interactive blocks, per-block analytics, quiz
  references, and structured querying. Rejected — see [ADR-0004](DECISIONS.md).
* **Normalized block-per-row:** maximally queryable but heavy CRUD/versioning overhead for little P1
  benefit. Rejected for now; JSONB + GIN covers query needs.
