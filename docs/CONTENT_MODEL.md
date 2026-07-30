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
| `heading` | section heading | `level` (2–4), `text` |
| `paragraph` | rich text | `text`, `marks` (bold/italic/code/link ranges) |
| `code` | code sample | `language`, `code`, `runnable` (reserved for Playground), `filename?` |
| `callout` | note/tip/warning | `variant` (info/tip/warning/danger), `text` |
| `image` | media | `mediaId`, `alt`, `caption?` |
| `quote` | blockquote | `text`, `attribution?` |
| `list` | ordered/unordered | `ordered`, `items[]` |
| `divider` | separator | — |
| `embed` | external embed | `provider`, `url` (allowlisted providers only) |
| `table` | simple table | `headers[]`, `rows[][]` |

Reserved for later phases: `quiz-ref` (P2), `exercise` (P3), `interactive-playground` (P3),
`file-download`, `math` (KaTeX).

**The Phase 1 catalog above is implemented and closed.** All ten types have renderers in
`packages/ui/src/blocks`, and the registry is typed `Record<BlockType, Component>` so adding a member
to `BlockType` fails the build until a renderer exists. Two fields remain reserved but unimplemented:
`paragraph.marks` (see below) and `code.runnable` (Playground, Phase 3).

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
  `paragraph.marks` is therefore reserved but deliberately unimplemented: rich text will arrive as a
  structured mark renderer, never as a raw HTML string.
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
