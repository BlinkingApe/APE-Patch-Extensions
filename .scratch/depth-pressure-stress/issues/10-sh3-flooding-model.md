# SH3's flooding model: how does APE provoke an astern leak?

Type: task
Status: open
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

## Question

The astern seep is **continuous while deep**: water accrues in a stern compartment at
the tier's rate every tick below the threshold, stops accruing on ascent, and is
pumped out by the crew normally. Nothing about how to make that happen is known.

Three sub-questions, in dependency order:

1. **Is compartment water a directly writable value?** Find the per-compartment
   water/flooding float (or whatever shape it takes) and confirm a write sticks
   rather than being recomputed by the engine each frame.
2. **Or is flooding derived from compartment damage?** If damaging a compartment
   causes the engine to generate flooding itself, that is the better mechanism —
   stock logic does the work, stock repair undoes it, and it is more "alongside" in
   spirit. Determine which of the two SH3 actually does; they may not be independent.
3. **Which compartment is "astern"?** The stuffing boxes are on the propeller
   shafts, which argues for the stern engine/motor room over the stern torpedo room.
   Confirm what compartments SH3 exposes and pick the one that reads right.

Also worth capturing while in there, because it decides whether the effect is even
playable: **the crew's pump rate**, so ticket 12 can choose a seep rate that is a
running cost rather than an unwinnable one.

HITL: needs a live boat, ideally one already flooding from a depth charge so the
value can be found by watching it change.

⚠ Hash the running `sh3.exe` first.

## Acceptance criteria

- [ ] The mechanism is decided: direct water write, or damage-the-compartment.
- [ ] The chosen value located, with anchor/chain/offset, and a write confirmed to
      stick and to be visible in the damage-control screen.
- [ ] The target compartment named.
- [ ] Crew pump rate observed and recorded, in units comparable to a seep rate.
- [ ] Written up in `docs/re-notes/` as a new note (this is a new subsystem, not an
      addendum to the crush-depth note).

## Notes

No anchor and no offset exist for this today. The only precedent for an
equipment/compartment damage value anywhere in this repo is the diesel float at
`[0x5543DC+4]+0x30C`, found by accident of h.sie's cave rather than by walking the
table — so expect this to be genuine discovery, not confirmation.
