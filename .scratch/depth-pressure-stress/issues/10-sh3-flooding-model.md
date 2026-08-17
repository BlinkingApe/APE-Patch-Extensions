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

## Static leads (2026-08-17): two open leads, starting from zero

A static pass (strings-first, since this ticket had no code anchor to trace from)
found:

- **Compartment naming.** SH3's own interior-camera table tags its sternmost
  renderable compartment `3704=DIESEL ENGINE ROOM;InteriorAft` — concrete evidence
  (not inference) favoring the engine room over the stern torpedo room for
  "astern," though the crew-station and equipment lists (`en_menu.txt` 245–256,
  458–496) track diesel and electric engines as distinct stations, so *which*
  engine room isn't settled.
- **An unconfirmed flooding read/compare.** A float `[ecx+0x1C]` is compared
  against `0.35` at `0x436BE4`, inside a function (`0x435B30`) that walks a
  compartment/subsystem linked list rooted at `[0x5449B0]→[+0xD8]→[+0x83C]` — a
  different root from `0x5543DC` (confirmed engine-specific elsewhere) — to gate
  the "We have flooding, sir!" message. `ecx`'s identity was not resolved
  statically; this is a lead for a live session to watch, not an anchor.
- **h.sie's `.pcode` caves have nothing here** — unlike hull repair or diesel
  damage, no h.sie mod touches flooding/water/pumps in this install, so there's no
  existing mechanism to borrow. No pump-rate value found either.

Full writeup, including what was ruled out: `docs/re-notes/06-flooding-model-
signature.md`.

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
