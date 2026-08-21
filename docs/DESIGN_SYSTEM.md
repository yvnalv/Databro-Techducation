# DataBro — Design System

The token and component vocabulary for both frontend apps. Page-level composition lives in
[UI_PATTERNS.md](UI_PATTERNS.md); the architectural rules for *where* tokens live are in
[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md).

## 0. Provenance and scope

The visual language is DataBro's own, set by [ADR-0020](adr/0020-design-language-and-the-contrast-rule.md).
The palette comes from the brand's existing presence — the colours already carrying DataBro's social
output — rather than from a reference product.

**Structure** (spacing, component anatomy, page composition, §3 onward) still derives from the
LearnUp reference set. Layout patterns are not protectable; that part of the original derivation
stands. **Colour and typography no longer do.**

> **Palette history.** The first revision used a teal/violet palette, on the reasoning that a blue
> LMS is hard to tell apart from its competitors. It was overridden to match a reference set, and
> this file recorded at the time that the teal reasoning was "the argument to revisit if DataBro ever
> wants visual separation from the category." ADR-0020 is that revisit. The record stays rather than
> being tidied away — the original instinct was right.

**A rendered version of this document exists.** Every token below appears as a swatch with its
measured contrast ratio, alongside each component in each state, at the design preview linked from
[ARTIFACTS.md](ARTIFACTS.md). When in doubt about what a token looks like, look at it.

---

## 1. Colour

### 1.1 The rule everything follows

**An accent is a fill, never a text colour.**

All three brand hues are high-luminance. Against the `#f0ece2` page they measure 1.5:1 (teal),
1.0:1 (lime) and 2.3:1 (purple) — none is legible as type. Against `#121212` they measure 10.6:1,
15.9:1 and 7.1:1, and near-black text *on* the teal is 11.9:1.

So each accent is a **triple**, not a value:

| Token | Job |
|---|---|
| `accent` | the fill |
| `accent-on` | what sits on that fill |
| `accent-strong` | the text-safe form — links, accent-coloured type, borders, **focus rings** |

`text-*`, `border-*` and `ring-*` take `-strong`. A focus ring in the raw brand teal would be 1.5:1
against the page, failing the 3:1 floor for a non-text indicator. There is deliberately no plain
`text-accent`: reaching for it is a miss, not a silent 1.5:1 label.

### 1.2 Brand

| Role | Hex | Use |
|---|---|---|
| `accent` | `#03dac6` | Primary fills: buttons, active nav, chips, progress |
| `accent-hover` | `#00c4b1` | Hover on a fill (lightens to `#4de8d9` in dark) |
| `accent-on` | `#121212` | Text and icons on an accent fill — 11.9:1 |
| `accent-strong` | `#00786a` | Links and accent type on a light ground — 5.6:1 |
| `accent-subtle` | `#d9f6f2` | Tinted backgrounds, soft buttons, chip fills |
| `accent-deep` | `#000000` | Footer, CTA band, inverted table heads |

`accent-deep` is pure black in **both** themes, so its text partner is `ink-on-deep` — a token that
deliberately does not flip. Using `ink-inverted` there would go dark-on-black once dark mode ships.

### 1.3 Secondary and premium

| Role | Hex | Use |
|---|---|---|
| `secondary` | `#c1ff72` | Lime. **Dark surfaces only** — 1.0:1 on the cream page |
| `secondary-on` | `#121212` | On a lime fill — 15.9:1 |
| `secondary-strong` | `#3f6212` | The rare light-mode need — 6.0:1 |
| `premium` | `#bb86fc` | Premium badge fill |
| `premium-on` | `#121212` | On it — 7.9:1 |
| `premium-strong` | `#6b21a8` | Premium type on light — 7.4:1 |

Lime is the clearest case of the §1.1 rule: it is not a weak colour, it is a *dark-surface* colour.
On the rail, on an inverse card, and in dark mode it is the strongest thing on the page.

### 1.4 Surfaces and neutrals

The neutrals are warm, derived from the cream so they sit *with* it rather than beside it.

| Role | Light | Dark | Note |
|---|---|---|---|
| `surface` | `#f0ece2` | `#121212` | Page background |
| `surface-raised` | `#ffffff` | `#1c1c1c` | Cards — they lift off the cream |
| `surface-sunken` | `#e6e1d5` | `#0a0a0a` | Bands, code blocks, table headers |
| `surface-inverse` | `#121212` | `#f0ece2` | **The emphasis surface** |
| `ink` | `#121212` | `#f0ece2` | 15.9:1 |
| `ink-muted` | `#5c574c` | `#a49e90` | 6.4:1 |
| `ink-subtle` | `#8a8478` | `#8b8375` | 3.3:1 — **meta and captions only, never body** |
| `ink-inverted` | `#f0ece2` | `#121212` | On `surface-inverse` |
| `ink-on-deep` | `#f0ece2` | `#f0ece2` | On `accent-deep`; does not flip |
| `line` | `#e0dacd` | `#2b2b2b` | Quiet by design |
| `line-strong` | `#cec7b7` | `#3f3f3f` | Inputs, emphasis |

`surface-inverse` is what replaced the gradient band. It is the device for emphasis, selection and
promotion, and in light mode it is the only place the raw teal and the lime appear at full strength.

### 1.5 Functional colours

Conventional hues in text-safe form, each with a subtle fill so a status can be tinted without
inventing a colour.

| Role | Hex | Subtle |
|---|---|---|
| `success` | `#15703f` | `#dcf0e4` |
| `warning` | `#92580a` | `#fbeeda` |
| `danger` | `#bf2718` | `#fbe0dc` |
| `info` | `#00786a` | `#d9f6f2` |

`info` is the accent's text-safe teal rather than a blue: a second cool hue beside the brand teal
reads as muddy, and *informational* is the brand's own register here.

### 1.6 Rules

* **Never colour-only.** Every colour signal is paired with text, an icon, or an ARIA role.
* **No gradients.** Removed outright by ADR-0020, not re-coloured. Emphasis is `surface-inverse`.
* **Fills get their `-on` partner.** `bg-accent` without `text-accent-on` is a bug.
* **`-strong` for anything that is not a fill.** Text, borders, focus rings.
* **Category tints** come from the `subtle` steps of existing hues, never from new colours.

---

## 2. Typography

### 2.1 Families

| Role | Family | Rationale |
|---|---|---|
| Display / headings | **Poppins** 600/700 | Geometric with a generous x-height |
| Body / UI | **Poppins** 400/500 | One family across both roles |
| Code | **JetBrains Mono** 400/500 | Unambiguous `0/O` and `1/l/I` — this is a coding-education product, and code blocks are the content |

Self-hosted via Fontsource, not fetched from Google's CDN: a third-party font request on every page
is both a privacy leak and a render-blocking dependency on the SEO-critical path. Poppins is not
published as a variable font, so the four weights are imported individually rather than shipping all
nine to every page.

**Poppins for body is a deliberate trade.** It is wider and rounder than a grotesque, which costs
efficiency at 13–15px in dense tables. The line heights in §2.2 run looser to compensate. If dense
data screens ever start to suffer, splitting body onto a grotesque is the escape hatch — and it is a
one-line change in the preset.

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

* Headings are **600–700 weight, `ink`, tight tracking**. Poppins carries weight well; 800 was
  dropped with the family change because Poppins at 700 already reads as heavy as the old face at 800.
* Article body is `lg` (18px), not `base`. Long-form reading wants the larger size.
* **Measure is ~68ch** for article bodies. The single most consequential number in the system.
* Section headings on marketing pages are **centred with a muted subtitle beneath**; content pages
  (article, category) are **left-aligned**. The reference is consistent about this and so are we.

### 2.4 Theme

Light and dark, switched by `DbThemeToggle` in the site header and both app shells. The choice is
stored in a `databro_theme` cookie shared across both apps, exactly like `databro_locale` — a
learner who switches to dark on the catalogue should not land in a light dashboard one click later.

**The theme is applied by a pre-paint script in `<head>`, never rendered into the SSR markup.**
`site` serves ISR-cached HTML, so a `data-theme` baked in at render time would be one visitor's
choice handed to everyone who hit the cache behind them. The script reads the cookie and stamps the
attribute before first paint, which also removes the flash of the wrong theme that the SSR approach
would still have had on any cached page.

Following `prefers-color-scheme` automatically stays deliberately unwired
([ADR-0020](adr/0020-design-language-and-the-contrast-rule.md) §6). The switch *is* the opt-in, and
the default is light.

---

## 3. Spacing and layout

### 3.1 Scale

Tailwind's 4px base. Component padding uses 12/16/20/24; section rhythm uses 48/64/80/96.

### 3.2 Containers

Two containers, and keeping them separate is the whole point: **widening the shell must never widen
the article body.**

| Container | Width | Gutter | Use |
|---|---|---|---|
| `.db-shell` | fluid, max **1760px** | 16 / 24 / 40px | The public **site**: listings, chrome, marketing |
| `.db-app-shell` | fluid, **no cap** | 16 / 20 / 24px | The authenticated **app** frame |
| `max-w-app` | **1760px** | — | The app's content column, beside the rail |
| `max-w-prose` | ~68ch (~680px) | 16 / 24px | **Article body only** |

**A content site and an application do not want the same frame.** A site centres its column and can
afford a wide gutter. An app cannot: its rail is *chrome*, and chrome belongs against the edge of the
window. Running the app through `.db-shell` put the rail about **90px** in from the viewport edge — a
40px gutter plus half of whatever the 1760px cap left over — which read as a page floating inside the
browser rather than an app filling it.

So the app frame is full-bleed with a small gutter, at **16 / 20 / 24px**: Material 3's body margins
are 16dp compact and 24dp expanded, and the reference set's rail sits at about the same inset. The
*content* is still capped, by `max-w-app` on the column beside the rail, so a card grid cannot sprawl
on an ultrawide display. **Chrome hugs the window; content stays readable.**

`.db-shell` is a class in `tokens.css`, not a repeated utility string, because container width is the
most-tuned value in any layout and should have exactly one definition.

**The app's two shells agree with each other.** They did not before: the learner shell capped at
`max-w-5xl` (1024px) and the CMS ran full-bleed, so the two halves of one product disagreed about how
wide a page is — visible as a dashboard floating in the middle of a 1860px screen beside a CMS that
filled it. Both now open with `.db-app-shell`.

**How the width was chosen.** The reference was measured, not eyeballed: content occupies **1220px in
a 1753px viewport** — a gutter of 265px each side, ratio **0.70** — and that holds across the blog
grid, the home course cards, the element page and the footer.

DataBro runs **wider than the reference**, because at 1200px the layout left conspicuous dead margin
on a large display. Resulting geometry:

| Viewport | Content starts at | Content width | Ratio |
|---|---|---|---|
| 1366 | 40px | 1286px | 0.94 |
| 1536 | 40px | 1456px | 0.95 |
| 1870 | 95px | 1680px | 0.90 |
| 2560 | 440px | 1680px | 0.66 |

The **cap matters as much as the width**. Fluid-to-infinity would stretch a card grid to absurd
widths on an ultrawide monitor; 1760px is where it stops. Ratios above 0.9 on common laptop widths
are intentional — this is a content site with card grids, not a text column.

**Column counts step up with the container**, otherwise the extra width just inflates each card:
listings and category tiles go 1 → 2 → 3 → **4** at `xl`. At 1870px that is a 402px card, which is
close to the reference's card size; three columns would have produced ~550px banners.

### 3.2b Reading layout — why the article does *not* just get wider

A centred 68ch column looks narrow on a 1870px display, and the instinct is to widen it. **Don't.**
The research is unusually consistent:

* Bringhurst's classic guidance, and [webtypography.net](http://webtypography.net/2.1.2), put the
  comfortable measure at **45–75 characters**; 66 is the most-cited optimum.
* [Baymard](https://baymard.com/blog/line-length-readability) and Dyson & Haselgrove find ~55–75 CPL
  supports the best comprehension; beyond ~80 the return sweep starts missing the next line.
* **WCAG 2.1 caps line length at 80 characters.** Widening the column to fill a 1870px viewport
  would put it near 100 — outside both the research range and the accessibility guidance.

The reference platforms do not widen the text either. DataCamp's content page is wide because it runs
**two columns** — a reading column of roughly 865px plus a ~250px sidebar — not because its lines are
long.

So DataBro does the same. On `xl` and above the article page is a grid:

| | |
|---|---|
| Reading column | `max-w-prose` (~68ch), unchanged |
| Gutter | 48px |
| Sidebar | 240px, sticky table of contents |
| Grid max | 64rem (1024px), centred |

At a 1870px viewport the article starts at **423px** instead of 595px — visually much wider — while
every line stays the same length. Below `xl` the sidebar is hidden and the column re-centres.

**The rule: use extra width for navigation, never for longer lines.** The same layout is what a
Phase 2 Lesson page should use.

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

A 12/16/24 family (ADR-0020 §5) — softer than the reference's ~6px, which is a deliberate departure.

| Token | Value | Use |
|---|---|---|
| `control` | 12px | Buttons, inputs, chips, menu items |
| `card` | 16px | Cards, tables, media, code blocks |
| `panel` | 24px | The rail, modals, page frames |
| `full` | 9999px | Avatars, search fields, pill badges |

`control` exists because 60 call sites were reaching for Tailwind's own 6px `rounded-md` default and
so bypassed the radius token entirely. There is no `sm` or `md`: a scale with steps nobody can tell
apart invites exactly that drift.

**One documented exception.** A checkbox keeps Tailwind's 4px `rounded`. At `rounded-control` a 16px
box is nearly a circle, which is what a radio button looks like — and shape is the only thing
separating the two controls. It is commented in place so the next audit does not "fix" it.

Everything else is on the scale, and that is checked by allow-list rather than by remembering: the
migration that introduced these tokens swept `rounded-md`/`sm`/`lg` and left 23 uses of bare
`rounded` behind, because a find-and-replace only fixes the names it was given (UI-5).

### 4.2 Shadow

**Soft and diffuse** — a wide, low-opacity spread rather than a tight dark drop. On a cream ground a
hard shadow reads as dirt. Cards are separated by their *fill* first, with a quiet hairline and the
shadow doing the rest.

| Token | Use |
|---|---|
| `card` | Resting cards |
| `lift` | Hover, dropdowns, popovers |
| `panel` | Modals, command palette |

`lift` replaced `card-hover`; `panel` is new, for the overlay surfaces the expanded component set
will need.

### 4.3 Motion

150ms ease-out for colour/shadow; 200ms for transforms. Cards lift on hover via shadow, not scale.
All of it inside `@media (prefers-reduced-motion: reduce)` guards.

---

## 5. Components

### 5.1 Buttons

Height 40px (`md`), radius `control`, weight 500–600, 150ms transitions.

Every filled variant names its own text token. `text-ink-inverted` on a teal fill would be cream on
`#03dac6` — 1.8:1, and invisible.

| Variant | Style |
|---|---|
| `primary` | `bg-accent` + `text-accent-on` — teal fill, near-black text |
| `secondary` | `bg-surface-inverse` + `text-ink-inverted` — the black button |
| `soft` | `bg-accent-subtle` + `text-accent-strong` |
| `outline` | `line-strong` border, `ink` text, transparent fill |
| `ghost` | No fill or border; `ink-muted` text |
| `danger` | `bg-danger` + `text-ink-inverted` (passes in both themes) |

`secondary` is the inverse surface rather than the lime. On a light page the second action is the
black button — which is what the references do, and the only way a second fill stays legible.

Sizes: `sm` 36px (`px-3.5`), `md` 40px (`px-5`), `lg` 48px (`px-6`). Focus is always a visible 2px
ring at 2px offset, drawn in **`accent-strong`** — never removed, and never in the raw brand teal
(§1.1).

**Use `DbButton`; do not re-create it.** Two pages once hand-rolled markup identical to the `lg`
variant, which meant a change to button padding or the focus ring would have moved every button on
the platform except those two. Both now go through the component (UI-6).

Note the spacing values above are real Tailwind steps. `px-4.5` is not one — the fractional scale
stops at `3.5` — and a class Tailwind cannot resolve emits nothing at all rather than warning, which
is how every `md` button once shipped with no horizontal padding (UI-1).

### 5.2 Inputs

40px height, radius `control`, 1px `line-strong` border, `surface-raised` fill. Focus:
`accent-strong` border + a 2px `accent-strong/30` ring. Labels sit **above** in `sm`/`ink-muted`. Optional leading icon (the reference's
search field). Error state: `danger` border plus a message — never colour alone.

### 5.3 Cards

Radius `card`, padding 24px, three tones:

| Tone | Style |
|---|---|
| `raised` | `surface-raised` fill, `line` hairline, `shadow-card` — the default |
| `inverse` | `surface-inverse` fill, `ink-inverted` text, `shadow-lift` — emphasis |
| `sunken` | `surface-sunken` fill, **no shadow** — it sits below, not above |

`inverse` is the device that replaced the gradient band, and the only place the raw teal and the lime
appear at full strength on a light page.

**The card is not a link** — the title inside it is, so the accessible name is the title rather than
the card's entire text content.

### 5.4 Chips and badges

Radius `full`, `xs` text, 600 weight. Two families, and the difference is deliberate:

**Tinted** (status) — a `subtle` fill with its text-safe hue. Sits quietly in a table or beside a
title.

| Kind | Style |
|---|---|
| Category | `accent-subtle` fill, `accent-strong` text |
| Tag | `surface` fill, `line` border, `ink-muted` text |
| Status | `{success\|warning\|danger\|info}-subtle` fill + matching text, always with a label |

**Filled** (brand) — the raw colour with its `-on` partner. These shout.

| Kind | Style |
|---|---|
| Accent | `bg-accent` + `text-accent-on` |
| Secondary | `bg-secondary` + `text-secondary-on` — legible precisely because it is a fill |
| Premium | `bg-premium` + `text-premium-on` |

### 5.5 Tabs

Two styles. **Soft** — active pill has an `accent-subtle` fill and `accent-strong` text.
**Underline** — active has a 2px `accent` bottom border and `ink` text. Use soft for filters,
underline for content sections.

The underline is one of the few places the raw `accent` appears without text on it: as a 2px rule
against the page it is a boundary, not a label, and it is paired with the label's weight change so
the state never rests on colour alone.

### 5.6 Accordion

Stacked cards with 8px gaps (not a bordered list). Header is `base`/600 with a chevron that rotates
on open. Body is `sm`/`ink-muted`. Must be a real `<button>` with `aria-expanded`.

### 5.7 Table

Radius `card` with an overflow wrapper — a wide table scrolls itself, never the page. Header row is
`surface-sunken`; the CMS list screens use `accent-deep` with `ink-on-deep` instead, which is the
inverted-head pattern reserved for dashboards. Rows separated by 1px
`line`. Cells 16px/12px padding, `sm`, `ink-muted`; first column `ink`.

### 5.8 Pagination

Numbered, crawlable `<a>` elements — never buttons. Active is `bg-accent` with `text-accent-on`;
others `ink-muted` with a `surface-sunken` hover. Prev/next chevrons. Accompanied by a "Showing X to Y of
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
