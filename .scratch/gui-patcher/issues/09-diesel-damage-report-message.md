# Diesel damage message: reword and move from pre-damage warning to post-damage report

Type: grilling
Status: resolved, built

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

h.sie's stock diesel-damage cave shows string 4893 ("If we don't cut power
now, we're risking a breakdown!") as a *warning*, gated on partial damage
only (`docs/re-notes/04-diesel-damage-signature.md`, `0x6351EE`/`0x6351F6`:
`cmp [edx], 1.0; je` skips the string call when damage is full/destroyed).
Raised by the user 2026-08-18 while confirming [ticket 05](05-exponent-format-live-check.md)
live: they want this changed to an in-universe **report** — the Chief
Engineer informing the Kaleun (player) that damage has occurred and why —
shown *after* the damage is applied, not as a pre-damage warning.

Two coupled sub-questions:

1. **Wording.** What does the Chief actually say — in-universe, attributed,
   informative about cause without breaking immersion.
2. **Mechanism.** Moving the call from "before/gated on partial" to "after,
   unconditionally" is a change to the cave itself (relocating or
   duplicating the string-display call past the damage-applying write,
   past the `je` gate) — plus wherever the new string text is sourced from
   (an existing unused string-table slot vs. a new APE-authored entry).
   This may need its own research/task pass on feasibility before it's
   buildable; not resolved here.

## Notes

Resolve the wording via `/grilling` now (in progress). The mechanism
question may spin off a separate ticket once the wording is settled.

## Answer

Resolved via `/grilling` 2026-08-18, prompted by observing the stock
warning fire live during ticket 05's test run.

1. **Mechanism correction — no relocation needed.** Re-reading the actual
   instruction sequence (`docs/re-notes/04-diesel-damage-signature.md:330-393`):
   the damage value is written to `[edx]` at `0x6351E0` (the `fstp`)
   *before* the TC-force/string-display block at `0x6351E2` onward runs.
   The string call is already positioned after damage is applied in code
   order — it only *reads* as a pre-damage warning because of h.sie's
   wording. Leaving the call site in place costs nothing; this is a text
   (and gate) change, not a relocation.
2. **Message text**: `"Diesel trouble, Kaleun. Normal wear and tear, we'll
   get her running, kein Stress."` — deliberately not tied to throttle/speed
   (APE's flattened-exponent model makes damage speed-independent above the
   idle cutoff, per ticket 12's finding A) and not implying loss (APE's
   shipped band `0.15…0.55` never destroys on a single hit).
3. **The `je` gate at `0x6351F4`** (currently skips the message on full
   destruction) is removed — the same message fires unconditionally on
   every hit, including the rare two-unrepaired-hits destruction tail
   (3.1%, per ticket 12). One message, no second string to author.

## Not resolved here (mechanism follow-up)

Resolved and built (2026-08-18): see [ticket 10](10-diesel-message-string-source.md)
for the mechanism and `src/patcher_gui/Manifest.cs`'s new `SiteStore.MenuText`
for the implementation. Originally open, left for whoever implements this:
where string 4893's *replacement* text is actually sourced from — overwriting the stock
string-table entry (risk: is 4893 referenced anywhere else in the game?
unchecked) vs. pointing `push 0x131D` at a new APE-authored string-table
entry. This is RE/asset-pipeline legwork, not a design decision, and
doesn't block scoping v1.1 — it blocks *building* this item.
