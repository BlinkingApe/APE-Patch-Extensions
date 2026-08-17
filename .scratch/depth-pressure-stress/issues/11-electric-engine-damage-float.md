# Locate the electric-engine damage float; confirm write and repairability

Type: task
Status: open
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

## Question

Depth wear damages **one** system: the electric engines — the historian's motor and
thrust bearings, and the system a deep submerged boat is actually running. Find the
value APE writes.

The precedent is close at hand: the diesel pair lives at `[0x5543DC+4]+0x30C`, and
`0x5543DC` is an engine-level singleton with 55 xrefs in stock `.text`, read by the
u-tanker mod, the diesel cave, and listed as an unidentified guard in
`01-crush-depth-damage-signature.md` §2. **The electric engines are plausibly a
neighbouring field on the same object** — cheap to check first before any scan.

Sub-questions:

1. Locate the electric-engine damage float; confirm the value's range convention
   matches diesel's (`0.0` healthy → `1.0` destroyed).
2. Confirm a write **sticks** and is reflected in the damage-control screen.
3. Confirm the damage is **repairable at sea** below some threshold, and find where
   that threshold actually is. Diesel proved `1.0` is terminal for the patrol; what
   is unknown is whether anything below `1.0` is uniformly repairable or whether
   there is a higher cutoff. This number **sets the hard ceiling** APE clamps to, so
   it is not optional detail.
4. Note whether the damage has gameplay consequences worth knowing for tuning —
   does partial electric-engine damage cap submerged speed, drain batteries faster,
   make noise?

HITL: needs a live boat and a crew to attempt repairs.

⚠ Hash the running `sh3.exe` first.

## Static leads (2026-08-17)

Confirmed the precedent this ticket's Question anticipated: `[[0x5543DC]+4]` is a
large object with fields at `+0x18`/`+0x1C` (the surface gate), `+0x30C` (diesel
pair, confirmed), `+0x320` (telegraph, confirmed), and eight further offsets used
by other, unidentified h.sie caves (`+0x14`, `+0x84`, `+0xA4` (a vtable call —
`call [eax+0xA4]`), `+0xD8`, `+0x358`/`+0x360` (doubles), `+0x438`/`+0x440`,
`+0x4A0`/`+0x4A4`). None of these is confirmed as the electric-engine pair — the
diesel floats sit at `+0x30C`/`+0x310` and nothing else in the tally lands
adjacent to that — so the short list above is a starting point for CE's
"find what accesses" on the electric-engine damage-control field, not a confirmed
answer. Full offset survey: `docs/re-notes/01-crush-depth-damage-signature.md` §6.

## Acceptance criteria

- [ ] Electric-engine damage float located; anchor/chain/offset recorded.
- [ ] Write confirmed to stick and to show in the damage-control screen.
- [ ] Repair threshold established empirically — the highest value from which crew
      repair still recovers the system at sea.
- [ ] A recommended **hard ceiling** proposed, with margin below that threshold.
- [ ] Written up alongside `docs/re-notes/04-diesel-damage-signature.md`'s findings
      on the same singleton, or as a new note if it turns out to live elsewhere.

## Notes

If the electric engines are a sibling field of the diesel pair on `0x5543DC`, this
ticket also cheaply advances the umbrella map's standing fog entry on identifying
that singleton — worth recording either way.
