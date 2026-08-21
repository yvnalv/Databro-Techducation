# DataBro — UI Defects

Visual defects found by **looking at the running application**. Distinct from
[OPEN_ITEMS.md](OPEN_ITEMS.md), which tracks scope, decisions and operational debt: this file tracks
things that are built, reachable, and wrong on screen.

It exists because the checks that gate this repository cannot see any of it. `lint`, `typecheck` and
the full test suite all pass on a page with an invisible button — Tailwind silently drops a class it
does not recognise, and no test asserts on styling. **The only detector for this category is a
person looking at the page.**

Open a defect with the surface it appears on, what is wrong, and the cause if it is known. Close it
by striking the row and naming the change that fixed it — a register that quietly loses entries is
worse than no register.

Last reviewed: 2026-08-20 (second pass).

---

## Open

| # | Surface | Defect | Cause |
|---|---|---|---|
| UI-3 | App dashboard — saved items | **Kind labels do not align into a column.** `LESSON` and `COURSE` are auto-width inline spans, so the titles beside them start at different x positions and the list reads ragged. | `apps/app/pages/index.vue` uses a flex row with `gap-x-3`. Needs a fixed-width label column or a two-column grid so the titles share a left edge. |
| UI-4 | Everywhere | **`ink-subtle` is faint on cream.** At 3.3:1 it clears the bar for meta and captions but is uncomfortable at `text-xs`, which is exactly where it is used most (the `LESSON`/`COURSE` labels, timestamps, card metadata). | Chosen deliberately in ADR-0020 to keep the hierarchy wide. Worth re-testing on a real screen at real distance; darkening it flattens the hierarchy, which is the trade. |

## Fixed

| # | Surface | Defect | Resolution |
|---|---|---|---|
| ~~UI-2~~ | Everywhere — 87 sites | ~~**Cards rendered cream-on-cream.** Any card-shaped element using bare `bg-surface` blended into the page instead of lifting off it.~~ | **Fixed — CHG-0066.** 87 surfaces lifted to `bg-surface-raised`; the 5 that are genuinely the page ground stay on `bg-surface`. Classified by whether the element has an edge or elevation of its own, then the non-matches reviewed by hand — which is how the 5 table bodies came out right, since a `<tbody>` inside a white card is white, not cream. |
| ~~UI-7~~ | Learner dashboard vs `/studio` | ~~**Three container widths in one product.** The learner shell capped at `max-w-5xl` (1024px), the CMS ran full-bleed, and `.db-shell` (1760px) was used only by the public site — so a dashboard floated in the middle of a 1860px screen beside a CMS that filled it.~~ | **Fixed — CHG-0066.** Both app shells open with `.db-shell`, the same 1760px cap the site uses. The rail sits inside that frame, so the app is capped as a whole. |
| ~~UI-8~~ | The rail, both shells | ~~**Every rail item rendered but nothing navigated.** Items showed their icon, label and active state, and clicking did nothing.~~ | **Fixed — CHG-0067.** `resolveComponent("NuxtLink")` was called *inside a template expression*, where it does not resolve — Vue falls back to treating the name as a native tag and emits a literal `<nuxtlink>`, which renders its children and is inert. `:is="'NuxtLink'"` as a bare string fails the same way. Resolved in `setup` instead. |
| ~~UI-9~~ | App frame, both shells | ~~**The rail sat ~90px from the window edge**, reading as a page floating in the browser rather than an app filling it.~~ | **Fixed — CHG-0067.** The app ran through the *site's* `.db-shell` (40px gutter + centring inside a 1760px cap). It now uses `.db-app-shell`: full-bleed, 16/20/24px gutter, with the content column capped by `max-w-app`. |
| ~~UI-1~~ | Every default-size button | ~~**No horizontal padding.** The label sat flush against both edges of the fill.~~ | **Fixed — CHG-0064.** `DbButton`'s `md` size was `px-4.5`. Tailwind's fractional spacing scale stops at `3.5`, so `px-4.5` is not a real class: it emitted no CSS and the padding silently became zero. Now `px-5`. Introduced by CHG-0063 an hour earlier and shipped past lint, typecheck and 454 tests, none of which can see it. |
| ~~UI-5~~ | Icon buttons, small controls, media tiles | ~~**23 radius classes off the token scale.** Bare `rounded` — Tailwind's 4px default — survived the ADR-0020 migration, which only swept `rounded-md`/`sm`/`lg`.~~ | **Fixed — CHG-0065.** 20 controls to `rounded-control`, 3 media surfaces to `rounded-card`. One exception stays and is commented in place: a 16px checkbox at `rounded-control` is nearly a circle, which is what a radio looks like. |
| ~~UI-6~~ | Hero, error page, save button | ~~**Buttons hand-rolled instead of using `DbButton`.** Two duplicated the `lg` variant exactly, so a change to button padding or focus ring would have moved every button on the platform except those two. A third was half a spacing step short.~~ | **Fixed — CHG-0065.** Hero and error page go through `DbButton`; `SaveButton` is `px-3.5` to match `sm`. |

---

## What this category costs, and the cheap detectors

Three of these share a shape: **a rename changed what a token means, and every call site relying on
the old meaning kept compiling.** UI-1 was a class that stopped existing. UI-2 is a class that still
exists and now means something else — the worse kind, because there is nothing at all to notice.
UI-5 is the third variant: a class the migration simply never looked for, because the sweep was
written against the names it knew about (`rounded-md`, `rounded-sm`, `rounded-lg`) and bare
`rounded` was not one of them.

That last one is the general lesson. **A find-and-replace only fixes what it was told to look for.**
The audits that found UI-2 and UI-5 worked the other way round — they enumerated what is *allowed*
and flagged everything else — which is why they caught cases the sweep had no name for.

Four checks would have caught these, and all are small enough to be worth committing:

1. **Class validity.** Scan templates for utility classes Tailwind cannot resolve — the fractional
   spacing steps are the obvious trap (only `0.5`, `1.5`, `2.5`, `3.5` exist). A class that emits
   nothing is always a bug, never a style choice.
1. **Token conformance.** Assert every radius, shadow and spacing class is one the design system
   defines, allow-list style. This is what caught UI-5, and it keeps catching the next one without
   anybody having to predict its name.
1. **No unknown elements in rendered HTML.** Fetch a page and assert it contains no lowercase custom
   tag the app never defined. UI-8 was a rail that rendered perfectly and navigated nowhere, and this
   one-line check finds it: a literal `<nuxtlink>` in the output is always a component that failed to
   resolve. It is also the only one of these four that needs the app *running*, which is precisely
   why nothing else caught it.
2. **Contrast pairing.** Assert that no fill colour is used as text, border or ring, and that every
   `bg-*` fill has its `-on` partner. This is O-8 in [OPEN_ITEMS.md](OPEN_ITEMS.md); it already
   caught two live 1.0:1 failures during the ADR-0020 migration that the rename sweep missed.

Neither replaces looking at the page. They just make the mechanical half automatic, so the looking
can be spent on judgement — alignment, rhythm, weight — rather than on finding classes that never
rendered.

---

## Related

* [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) — the token reference these defects are measured against.
* [ADR-0020](adr/0020-design-language-and-the-contrast-rule.md) — the change that introduced UI-1 and UI-2.
* [ARTIFACTS.md](ARTIFACTS.md) — the interactive design preview, which shows what these surfaces are
  *supposed* to look like.
* [OPEN_ITEMS.md](OPEN_ITEMS.md) — S-8 covers the remaining UI rework stages; O-8 covers the audit gate.
