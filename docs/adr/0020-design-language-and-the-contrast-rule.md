# ADR-0020 — The design language, and the rule that an accent is a fill

* **Status:** Accepted
* **Date:** 2026-08-20
* **Supersedes:** the palette and typography sections of [DESIGN_SYSTEM.md](../DESIGN_SYSTEM.md),
  which derived both from the LearnUp reference set.

---

## Context

The visual language was sampled from a third-party reference set: a primary blue `#0068d9`, a
pink→violet gradient band, a navy footer, Plus Jakarta Sans over Inter. `DESIGN_SYSTEM.md` §0 already
recorded unease about it —

> An earlier revision used a teal/violet palette, on the reasoning that a blue LMS is hard to tell
> apart from its competitors. That was overridden: the brief is to match the reference […] Recorded
> here because the teal reasoning is still the argument to revisit if DataBro ever wants visual
> separation from the category.

That moment arrived. The product owner has an established brand presence — the colours already
carrying DataBro's Instagram and TikTok output — and wants the platform to match it rather than the
reference.

The question of *when* was weighed against the pending staging deploy. Deploying does not make a
redesign more expensive by one file, and the codebase turned out to be in unusually good shape for
one: an audit found **zero** arbitrary hex values and **zero** stock Tailwind palette classes across
76 component files. Every colour already resolved through `tokens.css`. The debt was never in the
palette; it was in the small number of primitives, which is a separate problem.

## Decision

### 1. The palette

| Role | Value | Note |
|---|---|---|
| Page background | `#f0ece2` | Warm cream, used across both apps |
| Card surface | `#ffffff` | Cards lift off the cream; borders stay quiet |
| Ink | `#121212` | Near-black, not pure |
| Accent | `#03dac6` | DataBro teal — the brand colour |
| Secondary | `#c1ff72` | Lime |
| Premium | `#bb86fc` | Purple |
| Deep | `#000000` | Footer, CTA band, inverted table heads |

### 2. An accent is a fill, never a text colour

**This is the load-bearing decision and everything else follows from it.**

All three brand hues are high-luminance. Measured against the cream page:

| | vs `#f0ece2` | vs `#121212` | as a fill, with `#121212` on it |
|---|---|---|---|
| `#03dac6` teal | **1.5 : 1** | 10.6 : 1 | **11.9 : 1** |
| `#c1ff72` lime | **1.0 : 1** | 15.9 : 1 | **15.9 : 1** |
| `#bb86fc` purple | **2.3 : 1** | 7.1 : 1 | **7.9 : 1** |

None is usable as type on the page. All three are excellent as fills carrying dark text — which is
also how the reference screenshots use their accents, and how Material's dark-theme accents (from
which two of these three are drawn) were designed to be used.

So every accent is a **triple**, not a value:

* `accent` — the fill
* `accent-on` — what sits on that fill
* `accent-strong` — the text-safe form, for links and accent-coloured type on a light ground

`text-*`, `border-*` and `ring-*` must use `-strong`. This is not a stylistic preference: a focus
ring drawn in the raw brand teal is 1.5 : 1 against the page, which fails the 3 : 1 minimum for a
non-text UI indicator. It would have been an accessibility regression introduced by a palette swap,
and it would have been invisible in review because the class name still read `ring-accent`.

The migration renamed **132 call sites** across 46 files (`text-accent` → `text-accent-strong` ×70,
`ring-accent` → `ring-accent-strong` ×45, `border-accent` ×16, plus 6 fills whose text token would
have gone cream-on-teal).

### 3. Lime is a dark-surface colour

`#c1ff72` on cream is 1.0 : 1 — literally invisible. It is confined to `surface-inverse`, the rail
and dark mode, plus filled chips where the text is `secondary-on`. `secondary-strong` (`#3f6212`,
6.0 : 1) exists for the rare light-mode need. Two card-tint palettes were using `text-secondary` on a
pale fill and were corrected.

### 4. No gradients

Removed outright, not re-coloured: the `--db-gradient-*` stops, the `.db-gradient-band` class, and
its one call site. Gradients read as generated filler and the product owner rejected them by name.

The gradient was the only device the system had for emphasis, so it needed a replacement rather than
a deletion. That replacement is **`surface-inverse`** — a new token: near-black in light mode, cream
in dark. It is what the reference set actually uses, and it is the better device, because an inverse
block carries content where a two-stop band only decorates a strip.

`accent-deep` keeps a partner token, `ink-on-deep`, which does **not** flip with the theme —
`accent-deep` is pure black in both themes, so a flipping token would render dark-on-black the moment
dark mode ships.

### 5. Typography, shape, depth

* **Poppins** across display and body, at 400/500/600/700. Not a variable font, so the four weights
  are imported individually. JetBrains Mono stays: unambiguous `0/O` and `1/l/I` matter more on a
  coding-education platform than anywhere else.
* **Radius 12 / 16 / 24** (`control` / `card` / `panel`). `control` exists because 60 call sites were
  reaching for Tailwind's own 6px `rounded-md` default and so bypassed the radius token entirely —
  the one real leak in an otherwise disciplined system.
* **Soft, diffuse shadows** in three steps (`card` / `lift` / `panel`). On a cream ground a tight
  dark drop shadow reads as dirt.

### 6. Dark mode is designed, not yet shipped

The dark token set is complete and is where this palette is happiest — all three brand hues sit
directly on `#121212`, so `*-strong` collapses back onto the raw brand colour. It stays behind an
explicit `data-theme="dark"` opt-in and is deliberately **not** wired to `prefers-color-scheme`:
turning it on is a visible product change that deserves its own decision, and the previous revision
of this system was bitten by exactly that (a dark-OS visitor seeing a dark site the design never
intended).

## Consequences

* A palette change remains a one-file edit. The `-on` / `-strong` triples make the *safe* thing the
  named thing, so the next change cannot silently produce invisible text.
* `text-accent` no longer exists. Anyone reaching for it gets a build-time miss rather than a
  1.5 : 1 label — which is the point.
* Two shell widths disagreed by 600px (`.db-shell` at 1760px, an unused `maxWidth.shell` at 72rem).
  The unused one is deleted; `.db-shell` is now the only definition.
* The dead `brand.*` and `violet.*` ramps — 17 steps, zero call sites — are gone.
* **The design is applied, the layout is not.** This ADR covers tokens, type and primitives. The
  dynamic sidebar, the expanded primitive set (Select, Modal, Tabs, Table, Avatar, EmptyState,
  Toast) and the page-level composition drawn from the references are a separate, larger piece of
  work. Until it lands, pages keep their existing structure in the new skin.
* `info` is mapped to the accent's text-safe teal rather than to a blue. A second cool hue beside the
  brand teal reads as muddy. Flagged for review.
* `ink-subtle` is 3.3 : 1 — legal for meta and captions, not for body text. It must not migrate into
  running copy.

## Alternatives considered

**Keep `accent` as the text-safe teal and name the fill something else.** Would have avoided 132
renames, and the failure mode of getting it wrong is gentler (an off-brand dark button, rather than
invisible text). Rejected because `accent` should mean the brand colour; a system where `bg-accent`
and `text-accent` produce two different teals is a trap of a different shape.

**Darken `#03dac6` until it passes as text.** Rejected: it stops being the brand colour, which is the
entire reason for the change.

**Keep a gradient for emphasis, in the new hues.** Rejected by the product owner by name, and the
inverse surface is a stronger device regardless.
