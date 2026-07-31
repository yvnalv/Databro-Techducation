# DataBro — Design System

The token and component vocabulary for both frontend apps. Page-level composition lives in
[UI_PATTERNS.md](UI_PATTERNS.md); the architectural rules for *where* tokens live are in
[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md).

## 0. Provenance and scope

Structure, spacing, component anatomy and page composition are derived from the **LearnUp** reference
set (34 screenshots, `UI UX References/`). What was taken is the *design language* — layout rhythm,
component anatomy, interaction affordances. What was **not** taken:

* **The colour palette.** LearnUp is bright blue + a pink→violet gradient. DataBro uses a different
  palette (§1) — deliberately, both to differentiate from a generic LMS look and because DataBro's
  subject matter is technical rather than consumer-education.
* **Assets.** No illustrations, icons, photography or copy from the reference. Layout patterns are
  not protectable; artwork is.
* **Code.** The reference is Next.js + React + Bootstrap. Nothing transfers to Nuxt + Tailwind.

Values below were read visually from the screenshots and **rounded onto a scale**. They are
intentionally systematic rather than pixel-exact.

**Light mode is the primary theme.** Dark mode is supported (the token layer already resolves both)
but light is what gets designed and reviewed first.

---

## 1. Colour

### 1.1 Why not the reference palette

LearnUp's blue is the default LMS choice and its pink→violet gradient reads consumer/lifestyle.
DataBro teaches AI, data and software engineering to practitioners. The palette should read
*technical and credible* rather than *bootcamp bright*, and should not be mistakable for the dozen
other blue LMS products.

**Chosen direction: deep teal primary, violet secondary, amber for premium.** Teal is uncommon in
this category, carries data/terminal associations, and holds contrast well on white. Violet supplies
an AI-adjacent secondary without becoming the primary. Amber is reserved so "premium" always reads
the same way.

### 1.2 Brand ramp — teal (primary)

| Step | Hex | Use |
|---|---|---|
| 50 | `#f0fdfa` | Tinted backgrounds, chip fills |
| 100 | `#ccfbf1` | Hover on tinted surfaces |
| 200 | `#99f6e4` | Borders on tinted surfaces |
| 300 | `#5eead4` | Dark-mode accent text |
| 400 | `#2dd4bf` | Dark-mode accent |
| 500 | `#14b8a6` | Decorative only — fails AA on white for text |
| **600** | **`#0d9488`** | **Primary actions, links (light mode)** |
| 700 | `#0f766e` | Hover/pressed |
| 800 | `#115e59` | Deep fills, CTA bands |
| 900 | `#134e4a` | Rare, high-contrast fills |

`600` on white is ~4.8:1 — passes AA for body text. `500` does **not**; it is decorative only.

### 1.3 Secondary ramp — violet

| Step | Hex | Use |
|---|---|---|
| 50 | `#f5f3ff` | Category tints, soft banners |
| 100 | `#ede9fe` | Chip fills |
| 200 | `#ddd6fe` | Borders |
| 500 | `#8b5cf6` | Decorative, illustration |
| 600 | `#7c3aed` | Secondary actions, category accents |
| 700 | `#6d28d9` | Hover |

### 1.4 Neutrals — slate

Text and surfaces. `#0f172a` … `#f8fafc` (Tailwind `slate`). Chosen over pure grey because the
slight blue cast pairs with both teal and violet.

| Role | Light | Note |
|---|---|---|
| `surface` | `#ffffff` | Page background |
| `surface-raised` | `#ffffff` | Cards — separated by border + shadow, not fill |
| `surface-sunken` | `#f8fafc` | Alternating section bands, code blocks, table headers |
| `ink` | `#0f172a` | Headings and body |
| `ink-muted` | `#475569` | Secondary text, excerpts |
| `ink-subtle` | `#64748b` | Meta, timestamps, captions |
| `line` | `#e2e8f0` | Card and divider borders |
| `line-strong` | `#cbd5e1` | Input borders, emphasis |

Headings in the reference are a deep navy rather than black. DataBro keeps that: `ink` is
`#0f172a`, not `#000`.

### 1.5 Functional colours

| Role | Hex | Use |
|---|---|---|
| `success` | `#059669` | Confirmation, "published", check marks |
| `warning` | `#d97706` | Caution callouts |
| `danger` | `#dc2626` | Destructive actions, errors |
| `info` | `#0284c7` | Informational callouts |
| `premium` | `#b45309` (on `#fffbeb`) | Premium badge — the *only* amber in the system |

Callout variants map onto these: `info`, `tip` → teal `600`, `warning`, `danger`.

### 1.6 Rules

* **Never colour-only.** The reference leans on colour for status chips; DataBro pairs every colour
  signal with text, an icon, or an ARIA role. Callouts already do this via `role` + `data-variant`.
* **No gradients as brand carriers.** The reference's pink→violet page header is replaced by a solid
  `surface-sunken` band or a flat teal `800` band. Flat renders faster, prints and screenshots
  predictably, and does not fight the light theme. A single subtle teal→cyan gradient is permitted
  on the newsletter/CTA band only.
* **Category tints** (reference uses nine pastel tiles) come from the `50` step of teal, violet, and
  the functional hues — not from arbitrary new colours.

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
| `primary` | teal `600` fill, white text; hover `700` |
| `secondary` | violet `600` fill, white text |
| `soft` | teal `50` fill, teal `700` text — the reference's "light" buttons |
| `outline` | `line-strong` border, `ink` text, transparent fill |
| `ghost` | No fill or border; `ink-muted` text |
| `danger` | `danger` fill, white text |

Sizes: `sm` 32px, `md` 40px, `lg` 48px. The reference's `lg` on CTAs is the pattern to follow.
Focus is always a visible 2px ring at 2px offset — never removed.

### 5.2 Inputs

40px height, radius `md`, 1px `line-strong` border, white fill. Focus: teal `600` border + 2px
teal `100` ring. Labels sit **above** in `sm`/`ink-muted`. Optional leading icon (the reference's
search field). Error state: `danger` border plus a message — never colour alone.

### 5.3 Cards

White fill, 1px `line` border, radius `card`, `shadow-card`. Padding 24px. Hover raises to
`shadow-card-hover`. **The card is not a link** — the title inside it is, so the accessible name is
the title rather than the card's entire text.

### 5.4 Chips and badges

Radius `sm`, `xs` text, 500 weight, 8px/2px padding.

| Kind | Style |
|---|---|
| Category | teal `50` fill, teal `700` text |
| Tag | white fill, `line` border, `ink-muted` text |
| Status | Functional `50` fill + `700` text, always with a label |
| Premium | `#fffbeb` fill, `#b45309` text |

### 5.5 Tabs

Two styles, both in the reference. **Soft** — active pill has teal `50` fill and teal `700` text.
**Underline** — active has a 2px teal `600` bottom border. Use soft for filters, underline for
content sections.

### 5.6 Accordion

Stacked cards with 8px gaps (not a bordered list). Header is `base`/600 with a chevron that rotates
on open. Body is `sm`/`ink-muted`. Must be a real `<button>` with `aria-expanded`.

### 5.7 Table

Radius `card` with an overflow wrapper — a wide table scrolls itself, never the page. Header row is
`surface-sunken` (the reference's dark header is reserved for dashboards). Rows separated by 1px
`line`. Cells 16px/12px padding, `sm`, `ink-muted`; first column `ink`.

### 5.8 Pagination

Numbered, crawlable `<a>` elements — never buttons. Active is teal `600` fill, white text; others
`ink-muted` with a `surface-sunken` hover. Prev/next chevrons. Accompanied by a "Showing X to Y of
Z" count on dashboards.

### 5.9 Alerts / callouts

Left border 4px in the variant colour, `surface-sunken` fill, radius `card`, 20px/16px padding.
Variant conveyed by `role` (`note` / `alert`) and `data-variant` in addition to colour.

---

## 6. Accessibility floor

Non-negotiable, and where the reference is silent we are stricter:

* Body text ≥ 4.5:1, large text ≥ 3:1. Teal `500` is decorative only for this reason.
* Every interactive element has a visible focus ring.
* Colour never carries meaning alone.
* Links inside prose are **underlined**, not colour-only.
* Skip-to-content link, landmark elements, one `h1` per page, no skipped heading levels.
* Hit targets ≥ 44×44px on touch.
* Target WCAG 2.1 AA (per [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)).
