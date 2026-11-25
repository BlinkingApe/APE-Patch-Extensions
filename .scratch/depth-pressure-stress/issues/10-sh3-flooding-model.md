# SH3's flooding model: how does APE provoke an astern leak?

Type: task
Status: resolved
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

- [x] The mechanism is decided: direct water write, or damage-the-compartment.
- [x] The chosen value located, with anchor/chain/offset, and a write confirmed to
      stick and to be visible in the damage-control screen.
- [x] The target compartment named (VII/IX; II/XXI → ticket 19).
- [x] Crew pump rate observed and recorded, in units comparable to a seep rate.
- [x] Written up in `docs/re-notes/` as a new note (this is a new subsystem, not an
      addendum to the crush-depth note).

## Notes

No anchor and no offset exist for this today. The only precedent for an
equipment/compartment damage value anywhere in this repo is the diesel float at
`[0x5543DC+4]+0x30C`, found by accident of h.sie's cave rather than by walking the
table — so expect this to be genuine discovery, not confirmation.

## Lead from sh3-crush-pressure (2026-11-25): mechanism, stern value, compartment and pump rate

**vanilla Steam 1.4b; portability: owner.** Measured on the unwrapped vanilla Steam
SH3 1.4b exe, sha256 `1f14568235f903e7e50b73d3caa937b6946aadf7013e3dc39dfd1cc7975986f2`,
VIIC, realistic repair time on. Not OAE: every address below is for that build,
and porting it to OAE (and re-hashing) is the owner's call. This ticket is **not**
resolved by this section.

- **Mechanism: direct water write.** Each compartment has one zone state (an
  SH3Collisions object, vtable `SH3Collisions+0x1A520`): damage `+0x14`, breach
  `+0x20`, **flood level `+0x1C`**, float32, 0 dry to 1 full. A write to `+0x1C`
  holds, the crew pumps it like its own water, and the game reacts to it (depth
  rate, CE reports, F7 water). Verdict **authoritative** in three runs.
  - Damage-the-compartment was tested and rejected: the engine's flood integrator
    (`SH3Collisions+0x77B9`, `flood += breach / FloodingTime × dt`) only runs once
    the boat's damage controller is running, which needs a collision hit (a crush
    hit doesn't start it). Then it floods at a rate fixed by data (0.5 / 120 ≈
    0.0042/game-s).
- **Your static lead is confirmed, with one correction.** `0x00436BE4`
  `fld [ecx+0x1C]; fcomp [0.35]` reads this flood level; 0.35 is the "heavy
  flooding" report mark. The list's `next` is at `+0x4`, not `+0x2C`.
- **Chain.** List head `sh3.exe+0x1547D4` (VA `0x005547D4`): walk
  `node = [node+0x4]` until the zone state `[node+0x14]` has zone index
  `[[state+0x10]+0x8]` = 37 (or compartment byte `[0x00566898+zone]` = 8); the value
  is `[node+0x14]+0x1C`. Match by zone index, not list position. AOB
  `8B 3D D4 47 55 00 85 FF C7 44 24 40 00 00 00 00` at `0x0043601D` (unique; the
  dword at `+2` is the list head).
- **Compartment: VIIC compartment 8, stern torpedo room** (zone 37
  `TorpedoRoomStern`): the sternmost tile, the one owning propellers, rudders and
  stern planes; F7 draws its water in the sternmost section. **`InteriorAft` is
  ruled out as evidence:** vanilla labels it COMMAND ROOM; the diesel label is
  OAE's. By data, the astern compartment per class is II → 7, VII/IX → 8,
  XXI → 4 (only VIIC was run).
- **Pump rate: 0.0024314 flood per game-second** (one step a game-second) for the
  VIIC stern room's fresh crew; six measurements 0.00231–0.00243. Formula, matched
  to 7 digits: `factor × (crew strength + team strength) × 0.2/60` per step, only
  while 0 < flood < 0.99; factor 2 with realistic repair time off. Depth and
  damage don't change it; fatigue does (~0.0017 at strength 0.5). Crew strength:
  `[[[0x5449B0]+0xD8]+0x83C]+0x74+4×compartment`.
- **In play.** Seepage under the pump rate is a running cost (level 0–0.0024,
  occasional "We have flooding, sir!"); over it the race is lost at `rate − pump`.
  With way on a VIIC holds an ordered 150 m up to 0.8 stern water, at 6 → 5 → 4 kn.
  At 0.99 the room's men go down and nothing pumps it; the boat was lost ~70
  game-minutes later (cause not found). Stopped, any stern water sinks the boat.
  **Clamp APE's seepage well under 0.99** (ticket 12's proposal: 0.8).

Full write-ups, in the sh3-crush-pressure repo (`d:\code\sh3-crush-pressure`, branch
`automated-depth-re` until merged): `docs/re-notes/flooding.md` (§§1–6 the value,
§§12–15 the pump and play); finding records `docs/findings/stern-water.json` and
`docs/findings/pump-rate.json`.

## Answer (2026-11-25, OAE lab, sha256 `20360fe8…`)

Ported in one session with tickets 11 and 17: `docs/re-notes/08-oae-port-of-depth-leads.md`
§§1–2 (static), §7.1 (live).

- **Mechanism: a direct write to the compartment's flood level**, zone state `+0x1C`,
  0 dry → 1 full. On OAE a write of 0.5 held: the crew pumped it like its own water,
  F7 drew it, CE reported "We have flooding, sir!", and the boat got heavy. The
  damage-the-compartment route stays rejected (vanilla lead; the flood integrator
  `SH3Collisions+0x77B9` is byte-identical on OAE).
- **Chain, unchanged from vanilla:** list head `0x005547D4` (AOB
  `8B 3D D4 47 55 00 85 FF C7 44 24 40 00 00 00 00` @`0x0043601D`, unique on OAE). Walk
  `node = [node+0x4]` until `[[[node+0x14]+0x10]+0x8]` is the target zone; the value is
  `[node+0x14]+0x1C`. Match by zone index; OAE's `Zones.cfg` keeps the indices.
- **Target compartment, VII and IX: compartment 8, the stern torpedo room (zone 37).**
  By data: OAE's `Basic.cfg` has vanilla's compartment ids, and the vanilla lead saw it
  live on a VIIC. **Not seen live on OAE**: every OAE single mission spawned a IIA (note
  §7). **II and XXI have no compartment 8.** That choice is a new decision: ticket 19.
- **Pump rate: 0.003258 flood/game-s** at crew strength 1.0 (IIA, electric room),
  against the formula's 0.00333 (`factor × (crew + team strength) × 0.2/60`, factor 1
  with realistic repair time on; the pump code is byte-identical to vanilla, not
  h.sie-caved). The vanilla lead's 0.00243 was at a crew strength of ~0.73. A
  compartment with no crew isn't pumped at all.
