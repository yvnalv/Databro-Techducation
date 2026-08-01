# DataBro — Design System

The token and component vocabulary for both frontend apps. Page-level composition lives in
[UI_PATTERNS.md](UI_PATTERNS.md); the architectural rules for *where* tokens live are in
[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md).

## 0. Provenance and scope

Structure, spacing, component anatomy, page composition **and colour** are derived from the
**LearnUp** reference set (34 screenshots, `UI UX References/`). What was **not** taken:

* **Assets.** No illustrations, icons, photography or copy from the reference. Layout patterns are
  not protectable; artwork is.
* **Code.** The reference is Next.js + React + Bootstrap. Nothing transfers to Nuxt + Tailwind.

Colour values were **sampled from the screenshot pixels**, not estimated by eye — the primary blue,
the page-header gradient stops, the navy footer and the surface tints are measured. Spacing and type
values were read visually and rounded onto a scale.

> **Palette history.** An earlier revision used a teal/violet palette, on the reasoning that a blue
> LMS is hard to tell apart from its competitors. That was overridden: the brief is to match the
> reference, so the reference's blue is the palette. Recorded here because the teal reasoning is
> still the argument to revisit if DataBro ever wants visual separation from the category.

**Light mode only.** An earlier revision keyed dark mode off `prefers-color-scheme`, which meant a
visitor whose OS was dark saw a dark site the design never intended. That automatic switch has been
removed. `[data-theme="dark"]` remains as an explicit opt-in for a future toggle; nothing enables it
today.

---

## 1. Colour

### 1.1 Palette source

All values below are sampled from the reference screenshots. The system is built on **one blue**
(`#0068d9`), a **pink→violet gradient** used only on page-header bands, and a **deep navy**
(`#13293e`) that carries both headings and the footer.

### 1.2 Brand ramp — blue (primary)

| Step | Hex | Use |
|---|---|---|
| 50 | `#e7f1ff` | Tinted backgrounds, soft buttons, chip fills |
| 100 | `#cfe3ff` | Hover on tinted surfaces |
| 200 | `#a5cbff` | Borders on tinted surfaces |
| 300 | `#6fabff` | Decorative |
| 400 | `#2f88f5` | Decorative |
| 500 | `#0d74e6` | Decorative |
| **600** | **`#0068d9`** | **Primary actions, links, the CTA band** — the reference's brand blue |
| 700 | `#0057b8` | Hover/pressed |
| 800 | `#084a95` | Deep fills |
| 900 | `#13293e` | Navy — footer, headings, inverted bands |

`#0068d9` on white is ~4.6:1, which passes AA for body text.

### 1.3 Page-header gradient

Sampled left-to-right across the band: `#e377b1` → `#9274e4` → `#7a73f4`. Applied through the
`.db-gradient-band` class rather than a Tailwind utility, because it is one specific brand gradient
rather than a composable colour. **Only page-header bands use it** — it is not a general surface.

### 1.4 Neutrals

Sampled from the reference. The blue cast is deliberate — it is what makes the neutrals sit with the
brand blue rather than beside it.

| Role | Light | Note |
|---|---|---|
| `surface` | `#ffffff` | Page background |
| `surface-raised` | `#ffffff` | Cards — separated by border + shadow, not fill |
| `surface-sunken` | `#f6f8fd` | Alternating section bands, code blocks, table headers |
| `ink` | `#13293e` | Headings and body |
| `ink-muted` | `#5b6b7f` | Secondary text, excerpts |
| `ink-subtle` | `#8792a3` | Meta, timestamps, captions |
| `line` | `#e6ebf2` | Card and divider borders |
| `line-strong` | `#d5dde8` | Input borders, emphasis |

Headings in the reference are a deep navy rather than black, and the same navy carries the footer.
DataBro keeps both.

### 1.5 Functional colours

Taken from the reference's button set.

| Role | Hex | Subtle fill | Use |
|---|---|---|---|
| `success` | `#2e9e6b` | `#e4f3ee` | Confirmation, "published", **category chips** |
| `warning` | `#c2620e` | `#fff4e6` | Caution callouts |
| `danger` | `#d13415` | `#fbe2db` | Destructive actions, errors |
| `info` | `#0068d9` | `#e7f1ff` | Informational callouts |
| `premium` | `#b04a0c` | `#fdeae2` | Premium badge |

The category chip is **mint**, matching the reference: it reads as a label rather than a link, which
keeps it from competing with the blue title beneath it.

### 1.6 Rules

* **Never colour-only.** The reference leans on colour for status chips; DataBro pairs every colour
  signal with text, an icon, or an ARIA role. Callouts already do this via `role` + `data-variant`.
* **The gradient belongs to page-header bands only.** It is a brand signature, not a surface; using
  it elsewhere cheapens it and hurts text contrast.
* **Category tints** (reference uses nine pastel tiles) come from the `50` step of the brand, violet
  and functional hues — not from arbitrary new colours.

---

## 2. Typography

### 2.1 Families

The reference pairs a geometric, slightly rounded display face for headings with a neutral grotesque
for body. DataBro keeps that structure with open-licence equivalents:

| Role | Family | Rationale |
|---|---|---|
| Display / headings | **Plus Jakarta Sans** (600/700/800) | Geometric with friendly terminals — closest open equivalent to the reference's heading face |
| Body / UI | **Inter** (400/500/600) | Highest legibility at small sizes; already the project default |
| Code | **JetBrains Mono** (400/500) | Wide, unambiguous glyphs; already in use |

Both self-hosted, not fetched from Google's CDN — a third-party font request on every page is a
privacy leak and a render-blocking dependency on the SEO-critical path.

### 2.2 Scale

~1.25 (major third). Line heights are looser than Tailwind's UI defaults because this is a reading
platform.

| Token | Size | Line height | Use |
|---|---|---|---|
| `xs` | 12px | 18px | Meta, chip text |
| `sm` | 14px | 22px | Secondary text, table cells |
| `base` | 16px | 28px | Body (UI) |
| `lg` | 18px | 30px | **Article body** |
| `xl` | 20px | 30px | Lead paragraph, h4 |
| `2xl` | 24px | 32px | h3 |
| `3xl` | 30px | 38px | h2 |
| `4xl` | 36px | 42px | Page h1 |
| `5xl` | 48px | 52px | Hero |
| `6xl` | 60px | 64px | Hero (large screens) |

Display sizes tighten letter-spacing (`-0.02em` at `4xl`, `-0.03em` at `6xl`); body does not.

### 2.3 Rules

* Headings are **600–800 weight, `ink`, tight tracking** — matching the reference's confident,
  heavy headings.
* Article body is `lg` (18px), not `base`. Long-form reading wants the larger size.
* **Measure is ~68ch** for article bodies. The single most consequential number in the system.
* Section headings on marketing pages are **centred with a muted subtitle beneath**; content pages
  (article, category) are **left-aligned**. The reference is consistent about this and so are we.

---

## 3. Spacing and layout

### 3.1 Scale

Tailwind's 4px base. Component padding uses 12/16/20/24; section rhythm uses 48/64/80/96.

### 3.2 Containers

| Token | Width | Use |
|---|---|---|
| `prose` | ~68ch (~680px) | Article body |
| `shell` | 1200px | Listings, chrome, dashboards |
| Gutter | 24px (16px on mobile) | Both |

### 3.3 Section rhythm

| Context | Vertical padding |
|---|---|
| Marketing section | 80px (96px large screens) |
| Content page top/bottom | 56px (80px large screens) |
| Between cards in a grid | 24px |
| Between blocks in an article | 24px, with 48px above `h2` |

Marketing pages **alternate `surface` and `surface-sunken`** section backgrounds to separate bands
without borders. The reference does this consistently and it is what makes a long page readable.

### 3.4 Grid

| Content | Columns |
|---|---|
| Article/blog cards | 3 (2 tablet, 1 mobile) |
| Course cards | 4 (2 tablet, 1 mobile) |
| Category tiles | 3 (2 tablet, 1 mobile) |
| Footer | 5 (2 tablet, 1 mobile) |
| Article + sidebar | 8/4 split, sidebar collapses below |

---

## 4. Elevation, radius, motion

### 4.1 Radius

| Token | Value | Use |
|---|---|---|
| `sm` | 4px | Chips, small tags |
| `md` | 6px | Buttons, inputs |
| `card` | 8px | Cards, tables, code blocks |
| `lg` | 12px | Feature panels, banners |
| `full` | 9999px | Avatars, newsletter input, pill badges |

The reference is **moderately rounded, not pill-shaped** — buttons ~6px. Pills are reserved for
badges, avatars and the newsletter field.

### 4.2 Shadow

Cards are separated primarily by a **1px `line` border**, with shadow as secondary reinforcement.
This keeps a light-mode page calm.

| Token | Value |
|---|---|
| `card` | `0 1px 2px rgb(0 0 0 / .04), 0 4px 16px -4px rgb(0 0 0 / .08)` |
| `card-hover` | `0 2px 4px rgb(0 0 0 / .06), 0 12px 28px -8px rgb(0 0 0 / .14)` |

### 4.3 Motion

150ms ease-out for colour/shadow; 200ms for transforms. Cards lift on hover via shadow, not scale.
All of it inside `@media (prefers-reduced-motion: reduce)` guards.

---

## 5. Components

### 5.1 Buttons

Height 40px (`md`), padding 16px horizontal, radius `md`, weight 500–600, 150ms transitions.

| Variant | Light mode |
|---|---|
| `primary` | blue `600` fill, white text; hover `700` |
| `secondary` | violet fill, white text |
| `soft` | blue `50` fill, blue `700` text — the reference's "light" buttons |
| `outline` | `line-strong` border, `ink` text, transparent fill |
| `ghost` | No fill or border; `ink-muted` text |
| `danger` | `danger` fill, white text |

Sizes: `sm` 32px, `md` 40px, `lg` 48px. The reference's `lg` on CTAs is the pattern to follow.
Focus is always a visible 2px ring at 2px offset — never removed.

### 5.2 Inputs

40px height, radius `md`, 1px `line-strong` border, white fill. Focus: blue `600` border + 2px
blue `100` ring. Labels sit **above** in `sm`/`ink-muted`. Optional leading icon (the reference's
search field). Error state: `danger` border plus a message — never colour alone.

### 5.3 Cards

White fill, 1px `line` border, radius `card`, `shadow-card`. Padding 24px. Hover raises to
`shadow-card-hover`. **The card is not a link** — the title inside it is, so the accessible name is
the title rather than the card's entire text.

### 5.4 Chips and badges

Radius `sm`, `xs` text, 500 weight, 8px/2px padding.

| Kind | Style |
|---|---|
| Category | mint `#e4f3ee` fill, `#2e9e6b` text |
| Tag | white fill, `line` border, `ink-muted` text |
| Status | Functional `50` fill + `700` text, always with a label |
| Premium | `#fdeae2` fill, `#b04a0c` text |

### 5.5 Tabs

Two styles, both in the reference. **Soft** — active pill has blue `50` fill and blue `700` text.
**Underline** — active has a 2px blue `600` bottom border. Use soft for filters, underline for
content sections.

### 5.6 Accordion

Stacked cards with 8px gaps (not a bordered list). Header is `base`/600 with a chevron that rotates
on open. Body is `sm`/`ink-muted`. Must be a real `<button>` with `aria-expanded`.

### 5.7 Table

Radius `card` with an overflow wrapper — a wide table scrolls itself, never the page. Header row is
`surface-sunken` (the reference's dark header is reserved for dashboards). Rows separated by 1px
`line`. Cells 16px/12px padding, `sm`, `ink-muted`; first column `ink`.

### 5.8 Pagination

Numbered, crawlable `<a>` elements — never buttons. Active is blue `600` fill, white text; others
`ink-muted` with a `surface-sunken` hover. Prev/next chevrons. Accompanied by a "Showing X to Y of
Z" count on dashboards.

### 5.9 Alerts / callouts

Left border 4px in the variant colour, `surface-sunken` fill, radius `card`, 20px/16px padding.
Variant conveyed by `role` (`note` / `alert`) and `data-variant` in addition to colour.

---

## 6. Accessibility floor

Non-negotiable, and where the reference is silent we are stricter:

* Body text ≥ 4.5:1, large text ≥ 3:1. Brand steps below `600` are decorative only.
* Every interactive element has a visible focus ring.
* Colour never carries meaning alone.
* Links inside prose are **underlined**, not colour-only.
* Skip-to-content link, landmark elements, one `h1` per page, no skipped heading levels.
* Hit targets ≥ 44×44px on touch.
* Target WCAG 2.1 AA (per [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)).
