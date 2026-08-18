# Hull repair default range: 0-5%, and what changes with it

Type: grilling
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

APE's shipped default (`PatchState.cs`: `HullMin = 5, HullMax = 20`) is
currently h.sie's own stock roll, deliberately a no-op
(`Test_HullRepairAtHsiesOwnRangeIsNotAChange` in
`tests/patcher_gui/PatchSessionTests.cs` asserts ticking the box at the
default writes nothing). The user wants the real default changed to **0-5%**,
a genuine narrowing — meaning the box being ticked at defaults becomes a real
write, not a no-op.

Open sub-questions carried from [ticket 02](02-v1.1-polish.md)'s "Out of
scope for this note":

- **Range validity.** Is 0-5% valid under the same rules as any other
  player-set range (e.g. min <= max, both in [0,100])? Does 0 as a minimum
  need special-casing anywhere (PRD story 14 warns players when both bounds
  are zero for the *diesel* band — does an equivalent warning apply here for
  hull repair at 0% min)?
- **Review-list wording.** The plain-English review line for hull repair
  needs wording that makes sense when the default itself is now a real
  change, not just when a player edits away from default.
- **The no-op test.** `Test_HullRepairAtHsiesOwnRangeIsNotAChange` needs to
  change to still assert the no-op case exists, just at h.sie's own 5-20
  range instead of at APE's new default. Confirm this coverage is still
  wanted (it is — the no-op behavior itself isn't being removed, just moved
  off the default).

## Answer

Resolved via `/grilling` 2026-08-18:

1. **Range validity**: no change needed. `PatchState.IsValid` already
   accepts any `0 <= HullMin <= HullMax <= 100`, and `HullRepairsNothing`
   only fires when *both* bounds are zero — 0-5 doesn't trip it and needs
   no special-casing.
2. **Key finding — a behavioral change, not just a constant change.**
   `PatchSession.ReadBack()` (`PatchSession.cs:306-344`) currently reads
   hull's min/max straight off the exe bytes "in every state" (its own
   comment) — stock or APE-applied alike — because h.sie's stock 5-20 and
   APE's old default were the same numbers. On a fresh, unpatched install
   the exe still holds h.sie's stock 5-20 roll, so simply changing
   `PatchState.cs`'s `HullMin`/`HullMax` field initializers to 0/5 would be
   a no-op in practice: the UI would keep showing 5-20 until hull is
   applied. Hull repair needs to adopt the same conditional pattern diesel
   damage already uses (comment right below it): show APE's own default
   (0-5) when hull is *not yet* applied, and only read the live roll back
   off the exe once it *is* applied.
3. **Review-list wording**: unchanged. `Review.cs`'s `Hull()` already
   renders "The crew repairs X to Y%... instead of h.sie's 5-20%"
   generically off whatever `HullMin`/`HullMax` currently are.
4. **No-op test**: `Test_HullRepairAtHsiesOwnRangeIsNotAChange` gets
   rewritten to explicitly construct `desired` at h.sie's literal stock
   `HullMin=5, HullMax=20` (rather than relying on cloning `Current`, which
   will now hold APE's default 0-5 on an unapplied install) and assert the
   write-nothing behavior there.
