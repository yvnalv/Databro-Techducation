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

Last reviewed: 2026-08-20.

---

## Open

| # | Surface | Defect | Cause |
|---|---|---|---|
| UI-2 | Everywhere — 83 sites | **Cards render cream-on-cream.** Any card-shaped element using bare `bg-surface` now blends into the page instead of lifting off it. Most visible on the dashboard's saved-items list, which sits flush against the page while the enrollment cards above it are white. | `surface` used to be `#ffffff`, so `bg-surface` and `bg-surface-raised` were the same colour and the distinction never mattered. ADR-0020 made `surface` the cream page ground, and every site that meant "a white card" now means "the page". The fix is `bg-surface` → `bg-surface-raised`, but **it is not a blind replace**: 10 of the 93 uses are genuinely page-ground or input fills and must stay. |
| UI-3 | App dashboard — saved items | **Kind labels do not align into a column.** `LESSON` and `COURSE` are auto-width inline spans, so the titles beside them start at different x positions and the list reads ragged. | `apps/app/pages/index.vue` uses a flex row with `gap-x-3`. Needs a fixed-width label column or a two-column grid so the titles share a left edge. |
| UI-4 | Everywhere | **`ink-subtle` is faint on cream.** At 3.3:1 it clears the bar for meta and captions but is uncomfortable at `text-xs`, which is exactly where it is used most (the `LESSON`/`COURSE` labels, timestamps, card metadata). | Chosen deliberately in ADR-0020 to keep the hierarchy wide. Worth re-testing on a real screen at real distance; darkening it flattens the hierarchy, which is the trade. |

## Fixed

| # | Surface | Defect | Resolution |
|---|---|---|---|
| ~~UI-1~~ | Every default-size button | ~~**No horizontal padding.** The label sat flush against both edges of the fill.~~ | **Fixed — CHG-0064.** `DbButton`'s `md` size was `px-4.5`. Tailwind's fractional spacing scale stops at `3.5`, so `px-4.5` is not a real class: it emitted no CSS and the padding silently became zero. Now `px-5`. Introduced by CHG-0063 an hour earlier and shipped past lint, typecheck and 454 tests, none of which can see it. |

---

## What this category costs, and the two cheap detectors

Both defects above the line share a shape: **a rename changed what a token means, and every call site
that relied on the old meaning kept compiling.** UI-1 was a class that stopped existing; UI-2 is a
class that still exists and now means something else. The second kind is worse, because there is
nothing to notice.

Two checks would have caught them, and both are small enough to be worth committing:

1. **Class validity.** Scan templates for utility classes Tailwind cannot resolve — the fractional
   spacing steps are the obvious trap (only `0.5`, `1.5`, `2.5`, `3.5` exist). A class that emits
   nothing is always a bug, never a style choice.
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
