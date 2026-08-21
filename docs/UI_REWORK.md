# DataBro — UI Rework Plan

The staged plan for replacing the platform's visual language, and where it has got to.

The other UI docs answer different questions, and keeping them apart is the point:
[DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) and [UI_PATTERNS.md](UI_PATTERNS.md) are **reference** — what
the tokens and patterns *are*. [UI_DEFECTS.md](UI_DEFECTS.md) is what is **broken on screen**.
[OPEN_ITEMS.md](OPEN_ITEMS.md) is what is **owed, and by whom**. This file is the **sequence**: which
stage comes next and what it contains.

Last reviewed: 2026-08-20.

---

## Why it is staged

The work splits along a real seam. Stage A changes values that every component already reads through
a token, so it lands everywhere at once and costs almost nothing per file. Stages B–D add things that
do not exist yet, and cost per surface.

That ordering also front-loads the visible payoff: the platform looked different after a day, and the
expensive part — component coverage — is the part that keeps paying as Phase 3 adds screens.

---

## Stage A — Tokens, type, shape ✅ **Done**

Landed in **CHG-0063** ([ADR-0020](adr/0020-design-language-and-the-contrast-rule.md)), with fixes in
CHG-0064 … CHG-0067.

* Palette, typography (Poppins), a 12/16/24 radius family, three diffuse shadows.
* The rule the system runs on: **an accent is a fill, never a text colour**.
* Gradients removed platform-wide; `surface-inverse` replaced them as the emphasis device.
* Light/dark switch (**CHG-0065**), applied by a pre-paint script so the ISR cache cannot serve one
  visitor's theme to another.
* The collapsible rail and the app frame (**CHG-0066**, **CHG-0067**), plus 87 card surfaces lifted
  off the cream ground.

---

## Stage B — Icons, logo, favicon 🔜 **Next**

Nothing here exists yet. This is the smallest remaining stage and the most visible.

| # | Task | Notes |
|---|---|---|
| B-1 | **Choose an icon set** | Currently **8 files carry hand-written inline SVG**, and the rail's nine glyphs live in `AppRailIcon.vue`. Candidates: Lucide, Phosphor, Tabler. The choice is a *taste* decision — outline vs filled, stroke weight — and it should be made by looking, not by reading feature lists. |
| B-2 | **Wire it into `packages/ui`** | One component with a `name` prop, tree-shaken so a page ships only the glyphs it uses. `AppRailIcon.vue` is the single place the rail changes. |
| B-3 | **Create `public/` in both apps** | **Neither app has one.** There is no favicon, no `apple-touch-icon`, no OG fallback image, no `site.webmanifest`. |
| B-4 | **Logo** | `AppBrandMark` is a hand-drawn bar-chart glyph plus a wordmark, duplicated across both apps. Decide whether that is the mark or a placeholder. |

**Why B before C:** a favicon and a real icon set are what make the product stop looking like a
prototype, and neither blocks anything else. B is roughly a day.

---

## Stage C — The primitive set 🕐 **The one that compounds**

**6 primitives against 28 page files.** Most layout composition is bespoke Tailwind written inline
per page, which is where "consistent and reusable" is actually at risk — not in the palette, which is
fully tokenised.

Have: `DbButton`, `DbCard`, `DbChip`, `DbInput`, `DbAccordion`, `DbThemeToggle`.

| # | Missing primitive | Where it is being hand-rolled today |
|---|---|---|
| C-1 | `DbSelect` | `LocaleSwitch`, every studio filter |
| C-2 | `DbModal` / `DbDialog` | `MediaPicker`, delete confirmations |
| C-3 | `DbTable` | Six studio list screens, each with its own header/row markup |
| C-4 | `DbTabs` | Article editor, quiz builder |
| C-5 | `DbAvatar` | Three different initial-circle implementations |
| C-6 | `DbEmptyState` | Every list screen, each phrased differently |
| C-7 | `DbToast` | No feedback surface exists at all — saves are silent |
| C-8 | `DbPagination` | `PaginationNav` on site; studio re-implements it |
| C-9 | `DbSkeleton` | Loading states are mostly absent |

**Why this compounds:** every new screen re-invents layout, and Phase 3 (Billing, AI, Playground)
adds dozens. The cost of *not* doing C rises with every page added; the cost of doing it does not.

Do it by **extracting from existing pages**, not by designing in the abstract — a primitive invented
before it has three call sites usually has the wrong API.

---

## Stage D — Page-level layout 🕐 **Last**

Composition against the reference set, page by page: card grids, section rhythm, the right rail on
detail pages, table density.

Sized by how far the direction moves from current structure. Depends on C, because most of it is
assembling primitives that do not exist yet.

Open items already queued here: **UI-3** (the saved-items list does not align into a column) and
**UI-4** (`ink-subtle` reads faint at `text-xs`) — see [UI_DEFECTS.md](UI_DEFECTS.md).

---

## How to verify a stage

Four checks are cheap, and none of them replaces looking at the page. Each exists because something
got through the others:

1. **Class validity** — a utility Tailwind cannot resolve emits nothing and never warns (UI-1).
2. **Token conformance** — allow-list radii, shadows and spacing; a sweep only fixes names it was
   given (UI-5).
3. **Contrast pairing** — no fill colour used as text, border or ring; every `bg-*` has its `-on`
   partner (O-8).
4. **No unknown elements in rendered HTML** — a literal `<nuxtlink>` is a component that failed to
   resolve, and it renders perfectly while doing nothing (UI-8). The only check that needs the app
   running.

The [design preview](ARTIFACTS.md) is the reference for what a surface is *supposed* to look like.

---

## Related

* [ARTIFACTS.md](ARTIFACTS.md) — the interactive design preview.
* [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) — the token reference.
* [UI_PATTERNS.md](UI_PATTERNS.md) — page composition.
* [UI_DEFECTS.md](UI_DEFECTS.md) — what is currently wrong on screen.
* [OPEN_ITEMS.md](OPEN_ITEMS.md) — S-8 tracks this plan as one line; this file is the detail.
