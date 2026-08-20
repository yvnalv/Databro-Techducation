# DataBro — Published Artifacts

Live, hosted pages published alongside this repository: design previews, status briefings and
anything else that is easier to *look at* than to read as Markdown.

Each one is a **stable URL**. Republishing updates the page in place rather than minting a new link,
so a bookmark keeps working and the address below never goes stale. They are **private by default** —
visible to the account that owns them until deliberately shared from the page's own share menu.

Keep this file in sync the moment an artifact is published or retired. An index that quietly drifts
is worse than no index, for the same reason the changelog is immutable.

---

## Design

### Design Preview — tokens, type and components

**<https://claude.ai/code/artifact/efa2e6e1-6d5e-409f-91b3-a1316c6ea8af>**

The DataBro design language, rendered rather than described: the full token set as swatches with
their measured contrast ratios, the Poppins type scale, every component in every variant and state,
the radius and elevation families, and a working demo of the dynamic sidebar.

**Two things on the page are interactive.** The Light/Dark toggle, top right — the dark theme is
designed but not yet reachable in the apps, so this is currently the only place to see it. And the
**Collapse** button at the top of the sidebar, which drives the expand/collapse the rail will use.

*Use it when:* picking a token and needing to see what it actually looks like; checking whether a
colour pairing is legible before writing it; reviewing a design change before it reaches the code.

*Established:* the three rules the palette runs on — accents are fills rather than text colours,
lime belongs to dark surfaces, and there are no gradients. See
[ADR-0020](adr/0020-design-language-and-the-contrast-rule.md) for why, and
[DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) for the token reference this page visualises.

---

## Planning

### Build Ledger — milestones, open items and priorities

**<https://claude.ai/code/artifact/6529688e-679b-4acc-a2c3-5428ac06a439>**

Everything shipped across Phase 1 and Phase 2 grouped into milestones with their real `CHG` ranges,
everything still owed grouped by who has to act, the scope of the two phases not yet started, and a
recommended order of work with the reasoning for each step.

*Use it when:* deciding what to pick up next; needing the whole arc of the project in one view;
explaining to someone else where this stands.

*Compiled from* `CHANGELOG.md`, [ROADMAP.md](ROADMAP.md), [STATUS.md](STATUS.md),
[OPEN_ITEMS.md](OPEN_ITEMS.md), [DEPLOYMENT.md](DEPLOYMENT.md) and the ADRs — with test counts taken
from an actual run rather than from the docs.

> **This one goes stale.** It is a snapshot dated 20 August 2026, and unlike the design preview its
> contents age with every commit. [STATUS.md](STATUS.md) and [OPEN_ITEMS.md](OPEN_ITEMS.md) are the
> living record; treat the ledger as a periodic briefing rather than the source of truth.

---

## Conventions

* **One artifact, one URL, for its whole life.** Update by republishing the same file, never by
  creating a second page — an index full of near-duplicate links is how a reference stops being one.
* **Say what it is *for*.** The caption above each link exists so a future reader can tell whether
  the page answers their question without opening five tabs.
* **Mark the ones that expire.** A design preview stays true until the design changes. A status
  snapshot starts aging immediately. Readers need to know which they are holding.
* **Retire visibly.** If an artifact is superseded, keep its line and say what replaced it. Deleting
  it leaves a link in someone's notes pointing at nothing and no way to find out why.
