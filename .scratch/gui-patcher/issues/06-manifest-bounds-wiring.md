# Should the UI read validation bounds from the manifest instead of hard-coding them?

Type: grilling
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

Ticket 01's Out of Scope notes that the manifest's `min`/`max`/`default`
fields go unread — the window hard-codes its own bounds separately, and the
two can drift.

With [ticket 04](04-hull-repair-default-range.md) about to give hull repair a
*second* meaningful default (h.sie's own 5-20 as the no-op case, APE's new
0-5 as the shipped default), this is worth deciding now rather than letting
the drift compound:

- Should the window read `min`/`max`/`default` from the manifest at all, or
  is that scope creep for a polish release?
- If it should, how does a field with two meaningfully different defaults
  (hull repair's stock-vs-APE split) fit the manifest's single `default`
  field — does the manifest need a second default, or does "APE's default"
  stay a UI-layer constant separate from the manifest's `default` (which
  would then document h.sie's stock value only)?
- Is this in scope for v1.1 at all, or does it belong with the two-patch
  hardcoding cleanup that ticket 01 explicitly deferred?

## Answer

Resolved via `/grilling` 2026-08-18.

1. **Wire up `min`/`max`, yes.** The manifest already authors correct
   per-site bounds (`tools/re-static/gen_manifest.py`: e.g. hull width
   `vmin=1, vmax=101`, hull floor `vmin=0, vmax=100`, diesel bands
   `vmin=0.01, vmax=0.99`) but `Manifest.cs`'s `ReadSite()` never parses
   them into `Site`, and `MainForm.cs:167-168` hard-codes both hull
   spinners to the same literal `0, 100` regardless of which field
   (floor vs. width) they actually bound. Read `min`/`max` off each
   `Site` and feed them into the widget constructors instead of literals
   — closes the drift gap ticket 01 flagged and fixes the latent
   floor/width bound mismatch as a side effect.
2. **Leave `default` unread.** The manifest's `default` is authored from
   the *proven stock roll* (provenance/proof artifact, per the
   fixed-offset model) — it can only ever encode h.sie's stock value,
   not a product opinion about what APE should ship. Wiring it up would
   conflate those two things, and the stock-vs-APE split ticket 04
   introduced for hull repair only exists for one patch so far, not a
   reason to grow the schema. "APE's default" stays exactly what it is
   today: the UI-layer constant in `PatchState.cs`. No new manifest
   field.
3. **In scope for v1.1, narrowly.** The min/max wiring is a handful of
   lines (two fields parsed in `ReadSite()`, two literals swapped per
   spinner) and doesn't touch the two-patch structural duplication
   (still one hard-coded hull block, one diesel block) that ticket 01
   explicitly deferred to "when a third patch is actually specced."
   That duplication stays out of scope; this narrow fix ships with
   v1.1.
