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

## Static lead (2026-08-17, from a pass targeting ticket 08): `+0x358`/`+0x360` reassigned as a possible wear-ratio pair

A static pass chasing ticket `08`'s mission clock traced the `+0x358`/`+0x360`
doubles this ticket's §6 offset survey had left unidentified, and found them to be
a **bounded max/current accumulator** (`fmul`/`fadd`/`fst`/`fcomp` against a cap,
gated on the same `[0x5543DC+0x10]` liveness tolerance §6.2 already uses) —
structurally a wear/damage-ratio pair, not a clock. Not confirmed to *be* the
electric-engine float (doubles, not the diesel pair's floats; a different pair
shape), but close enough in structure to be worth a look before the from-scratch CE
scan this ticket already calls for. Full disassembly:
`docs/re-notes/01-crush-depth-damage-signature.md` §8.

## Static lead (2026-08-17, second pass targeting this ticket directly): most of the offset survey was mis-attributed; `+0x358`/`+0x360` remains the only lead

Re-walked the `0x5543DC` offset survey with a stricter register tracker and found
most of the "unidentified" offsets in the first static-leads note above **don't
actually belong to this object** — `+0x84`/`+0xA4` and `+0x4A0`/`+0x4A4` are off two
different singletons entirely (a stale-register scan artifact), `+0xD8` never
existed, `+0x83C` is the already-known god-object field, and `+0x14` is the
already-known liveness-tolerance field under a different guise. Corrected list:
`+0x000/+0x004/+0x008/+0x30C/+0x320/+0x358/+0x360/+0x438/+0x440` are the only real
fields off `[[0x5543DC]+4]`.

Chased the two remaining new candidates directly:

- **`+0x438`/`+0x440`**, structurally the most diesel-like shape left (a pair 8
  bytes apart) — ruled out. `+0x440` is a byte flag, not a float; `+0x438` is
  force-set to a fixed `0.5` on one transition, no clamp idiom. It's a
  torpedo-reload state patch (nearest message: "Reloading external torpedoes"), not
  engine damage.
- **String IDs 462/463** ("Stb/Port Electric Engine") — searched every `push`/`mov`
  form used elsewhere in these caves for message strings. **Zero hits anywhere in
  the binary**, unlike diesel's `460`/`461` (3 hits) or the flooding/torpedo
  messages found in other tickets' passes. There is no UI-string anchor to chase
  for electric engines in this build.

**Net: `+0x358`/`+0x360` (flagged in the ticket-08 pass above) remains the only
static lead, neither promoted nor demoted by this pass.** Everything else on the
shared object is now ruled out for a specific, citable reason rather than left
open. Full writeup: `docs/re-notes/01-crush-depth-damage-signature.md` §9.

## Live lead (2026-08-18): a repair-tracking float pair found, then ruled out

Live session (hash `EEEF8595A75D2C95D768867F7C3EDDEA48B7940BD3D82CA2D159857476C68B61`,
matches the recorded build). After a DC hit damaged the electric engines (~2 min
repair), scanned for values near `0.1`, then narrowed by "decreased value" across a
partial repair: two adjacent floats survived, `0x240C73E0`/`0x240C73E4` (4 bytes
apart), tracking down from ~`0.071`/`0.077` toward ~0 in step with the in-game
repair countdown — behaviorally a perfect match for a damage-fraction pair.

Computed their offset from `[[0x5543DC]+4]` anyway (`+0x213A2A58`/`+0x213A2A5C`) to
check the "sibling of diesel" hypothesis. The offset is ~554,000,000 bytes — far too
large to be a field inside the same allocation as the diesel pair (`+0x30C`), so
these two floats are **not** on `[[0x5543DC]+4]`'s object; the resemblance to the
diesel/telegraph cluster doesn't hold for this pair. (`+0x358`/`+0x360` remains the
only surviving lead on that specific object, per the second static pass above —
this live pass neither confirms nor rules that one out.)

Write-tested directly on the raw addresses regardless, since behavior alone looked
promising: writing `1.0` to `0x240C73E0` produced **no observable in-game effect**
and **did not hold** — read back as reverted. Same object class as the hull-HP
mirror at `0x00566A28`: something else recomputes or owns this value every tick.
**Ruled out as a red herring.** These addresses are also session-local raw
addresses regardless (not reached via a stable chain), so nothing here survives to
the next relaunch even setting aside the negative write result.

Net: still no located electric-engine damage float. `+0x358`/`+0x360` is the only
lead left standing from any pass, static or live.

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
