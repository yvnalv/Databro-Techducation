# DataBro — UI Patterns

Page-level composition, derived from the LearnUp reference set (`UI UX References/`, 34 screenshots).
Component and token vocabulary lives in [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md).

Each pattern below records **what the reference does**, then **what DataBro does** — they differ
wherever the reference conflicts with an existing DataBro rule (SEO, accessibility, or the fact that
DataBro is content-first rather than course-first today).

---

## 1. Global chrome

### 1.1 Header

Reference: sticky white bar. Left — logo mark + wordmark. Centre — nav items with dropdown carets
(Home, Courses, Pages, Accounts). Right — a secondary outline/pill action ("Become a Tutor") and a
primary filled action with icon ("Sign In"). On dashboards the right side becomes "Create Course" +
avatar.

DataBro:

* Same three-zone structure. Nav: **Articles, Categories, About**; right: **language switcher**, then
  `Sign in` (primary) once auth reaches the site.
* Sticky with a translucent backdrop blur and a bottom `line` border — already implemented.
* Height 64px, `shell` container.
* Dropdowns are real `<button aria-expanded>` menus, keyboard-navigable, closing on `Esc`.

### 1.2 Page header band

Reference: full-width **pink→violet gradient** band under the header. Centred white title; sometimes
a subtitle; sometimes a breadcrumb above the title (`Home / All Elements`).

DataBro: **the gradient band, as in the reference**, with centred white title and optional subtitle.
Stops are sampled values applied via `.db-gradient-band` (DESIGN_SYSTEM §1.3).

Used on **index-style pages** (category, tag). The **article page has no band** — a full-bleed header
pushes the body below the fold and costs reading time on the page that matters most. The **home page**
uses its own hero instead, matching the reference, whose home is light rather than gradient.

Breadcrumbs sit above the title, `sm`/`ink-subtle`, with the current page unlinked — and are mirrored
by `BreadcrumbList` JSON-LD on category pages.

### 1.3 Newsletter CTA band

Reference: full-bleed blue band with decorative circles, centred heading + subtitle, and a pill input
with an inset dark button. Appears above the footer on **every** page.

DataBro: same anatomy, teal `800` fill (or the one permitted subtle gradient), pill input, inset
`ink`-fill button. **Not on every page** — omitted from the article body page, where the priority is
finishing the article and following an internal link, and omitted from error pages.

### 1.4 Footer

Reference: dark navy (`#161d2f`-ish). Five columns — brand/address/contact, Navigations, Categories,
Help & Support, app-store buttons. Bottom bar with copyright and social icons.

DataBro: same dark treatment and column structure, minus the app-store column (no apps).
Columns: brand + contact, **Learn** (articles, categories, tags), **Company** (about, contact),
**Legal** (privacy, terms). Social icons in the bottom bar. Dark footer is intentional even in light
mode — it terminates the page.

---

## 2. Article list (`/`, `/categories/{slug}`, `/tags/{slug}`)

Reference ("Latest Updates"): gradient band, then a **3-column card grid**, then a centred soft
"Load More" button.

Card anatomy, top to bottom:

1. Cover image, full-bleed, ~16:9, rounded top corners
2. Category chip (soft tint)
3. Title — 600 weight, `ink`, up to 2 lines, the link
4. Excerpt — `sm`/`ink-muted`, 2 lines, ellipsis
5. Footer row — circular avatar (32px) + author name (600) + date (`xs`/`ink-subtle`) on the left,
   read time on the right

DataBro:

* Same card anatomy. Cover images are **optional** — the Media module does not exist yet, so cards
  must look deliberate without one (the current placeholder pattern applies).
* **Pagination, not "Load More".** Crawlers cannot press a button; this is settled in
  [SEO.md](SEO.md) and the reason listings are offset-paged.
* Grid is 3-up on `shell`; currently 2-up because the existing layout is narrower — moving to
  `shell` is part of the implementation plan.
* The dashboard reference's numbered pagination component is the one to reuse here.

---

## 3. Article detail (`/articles/{slug}`)

Reference: gradient band with the title, then an **8/4 split** — article card left, sidebar right.
Article: hero image, meta row (author, comments), body, pull-quote, author bio card, comments,
comment form.

Sidebar: Search, Categories (name + count), Trending Posts (thumbnail + title + relative time),
Tags Cloud.

DataBro:

* **Two columns on `xl` and above: the reading column plus a sticky table of contents** — the same
  shape as the DataCamp reference, and the reason its content page looks wide (DESIGN_SYSTEM §3.2b).
  The reading column keeps its ~68ch measure; the extra width carries *navigation*, never longer
  lines. Below `xl` the sidebar is hidden and the column re-centres.
* The reference's sidebar content (search, categories, trending) is **not** adopted — that is
  browsing, and it belongs below the article, where a finished reader is. The contents list is
  different: it serves the article you are currently in.
* The TOC lists `h2` and `h3` only; an `h4` outlines rather than navigates. Its anchor ids come from
  the **same** `headingAnchor` the renderer stamps onto the heading — two implementations would drift
  and every link would scroll nowhere, so a test asserts they agree.
* It appears only with **two or more headings**: a contents list of one is noise.
* Title, summary/lead, then a meta row (author, read time, date), then a rule, then the body — the
  current implementation, which stays.
* **Pull-quote** — adopted; already the `quote` block.
* **Author bio card** — adopted, below the article. Runs horizontally rather than the reference's
  centred column: there are no social links to show, and horizontal costs less vertical space between
  the end of the article and the related links, which is where a finished reader should go. Renders
  nothing at all when the author has no bio — a card with a name and empty space is worse than none.
* **Related articles** — the replacement for the sidebar. Same category, current article excluded,
  three across, at `shell` width rather than the prose measure because this is scanning, not reading.
* **Comments** — Phase 4 (Community module). Not built now.
* Related/topic links close the page: category chip + tag chips, already implemented.

---

## 4. Marketing home (`/`)

Reference section order:

1. **Hero** — pill badge, `5xl`/`6xl` heading, sub-paragraph, checkmark list, two CTAs (primary +
   outline), image collage with a floating stat card
2. **Logo strip** — "Learn from…" + greyscale brand logos
3. **Course grid** — centred heading + subtitle, soft filter tabs, 4-up cards
4. **Membership banner** — full-width tinted rounded panel, text left, image right, dark button
5. **Category tiles** — 3×3 grid of pastel tiles (icon, name, count)
6. **Instructors** — `surface-sunken` band, carousel of profile cards
7. **Why us** — 2-column text + checkmarks + CTA | image collage
8. **Pricing** — monthly/yearly toggle, 3 cards, middle emphasised, feature lists with check/cross
9. **Contact** — 2-column text + form
10. Newsletter band, footer

DataBro, **staged to what actually exists**:

* **Now:** hero (heading, sub, primary CTA), latest articles grid, category tiles, newsletter band,
  footer. Every one of these is backed by real data today.
* **Phase 2 (Learning):** course grid, instructors, pricing.
* **Never as-is:** the logo strip, unless there are real logos to show. A fake social-proof strip is
  worse than none.

The rhythm to keep regardless: **centred section heading + muted subtitle, alternating `surface` /
`surface-sunken` bands, generous 80–96px section padding.**

---

## 5. Category tiles

Reference: 3×3 grid; each tile a distinct pastel fill with a small illustrated icon, the name in a
colour matching the tint, and a class count beneath.

DataBro: same anatomy, tints drawn from the `50` step of the teal/violet/functional hues rather than
arbitrary pastels (DESIGN_SYSTEM §1.6). Tint is assigned **deterministically from the category slug**
so a category keeps its colour across pages and between deploys.

Two rules the reference does not have to worry about, because it has no real data:

* **Counts are published-only**, and come from a batched query rather than one per tile. A tile must
  never promise drafts a reader cannot open.
* **Any category with its own published articles is tiled, at whatever depth** — not just top-level
  ones. Restricting to top-level hides everything when articles live in child categories, which is
  the normal shape of a growing taxonomy. Rolling child counts up into the parent was rejected
  outright: a tile would advertise 28 articles and the page it links to would show 0, because the
  category page filters strictly. **A count must always agree with the page it points at.** Rolling
  up would first require changing what a category page *means* — a deliberate decision, not a display
  tweak.

---

## 6. Error pages

Reference (404): oversized ghosted `404` numeral, condensed "PAGE NOT FOUND" overlaid, muted
explanatory paragraph, primary "Back To Home" button. Newsletter band + footer still present.

DataBro: same composition, `line` grey for the ghost numeral. **No newsletter band on error pages.**
Already `noindex,nofollow`, and localized in both locales. The pattern generalises to 500/503 by
swapping the numeral and copy.

---

## 7. Dashboard shell (CMS — next slice)

Reference (instructor dashboard): gradient band with a **profile card overlapping it**, then a
sidebar + main split.

* Sidebar card: avatar, name + verified badge, rating, a two-stat row, then a vertical nav list with
  icons. Active item has a soft fill and coloured text.
* Main: breadcrumb, a row of 3 stat cards (icon in a tinted circle, large number, label), then a data
  table card.
* Table: **dark header row**, white text; rows with thumbnail + linked title, a soft status chip, and
  edit/delete icon buttons in tinted squares. Footer has "Showing 1 to 8 of 20 entries" and numbered
  pagination.

DataBro CMS mapping (**shell, auth and article list implemented**; editor next):

* Same sidebar + main shell, **without** the gradient/overlap flourish — the CMS is a tool, and the
  overlap costs vertical space on the surface where density matters.
* Sidebar nav: Dashboard, Articles, Taxonomy, Media, Settings. Active item = teal `50` fill, teal
  `700` text.
* Stat cards map to: total articles, published, drafts.
* The data table is the **article list**: title, status chip, category, author, updated, actions.
  Dark header row adopted here (its one sanctioned use, per DESIGN_SYSTEM §5.7).
* Row actions: edit (soft teal), delete (soft red) — both real buttons with accessible names, not
  bare icons.

---

## 8. Forms

Reference: label above, full-width input, 2-column for short pairs (name/email), textarea for
messages, primary submit button, left-aligned.

DataBro: same, plus rules the reference does not show — errors rendered beneath the field in
`danger` with `aria-describedby`, the field marked `aria-invalid`, and a summary at the top of the
form on submit failure so the error is announced. This matches the existing
`validation_failed` envelope, which already returns per-field details
([ERROR_HANDLING.md](ERROR_HANDLING.md)).

---

## 9. What is deliberately not adopted

| Reference pattern | Why not |
|---|---|
| "Load More" button | Not crawlable; listings use numbered pagination — SEO.md |
| Article sidebar | Competes with the 68ch measure on the page that matters most |
| Newsletter band on every page | Omitted on article and error pages |
| Logo/social-proof strip | Only with real logos |
| App-store download column | No apps exist |
| Comments | Phase 4, Community module |
| Instructor/tutor surfaces | DataBro is not a marketplace; no instructor economy in the roadmap |
