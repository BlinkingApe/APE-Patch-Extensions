# Locate the electric-engine damage float; confirm write and repairability

Type: task
Status: resolved
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

- [x] Electric-engine damage float located; anchor/chain/offset recorded.
- [x] Write confirmed to stick and to show in the damage-control screen.
- [x] Repair threshold established empirically — the highest value from which crew
      repair still recovers the system at sea.
- [x] A recommended **hard ceiling** proposed, with margin below that threshold.
- [x] Written up alongside `docs/re-notes/04-diesel-damage-signature.md`'s findings
      on the same singleton, or as a new note if it turns out to live elsewhere.

## Notes

If the electric engines are a sibling field of the diesel pair on `0x5543DC`, this
ticket also cheaply advances the umbrella map's standing fog entry on identifying
that singleton — worth recording either way.

## Lead from sh3-crush-pressure (2026-11-25): damage value, range, repair threshold and consequences

**vanilla Steam 1.4b; portability: owner.** Measured on the unwrapped vanilla Steam
SH3 1.4b exe, sha256 `1f14568235f903e7e50b73d3caa937b6946aadf7013e3dc39dfd1cc7975986f2`,
VIIC, realistic repair time on. Not OAE: every address below is for that build,
and porting it to OAE (and re-hashing) is the owner's call. This ticket is **not**
resolved by this section.

- **The value: two floats, one per engine**, the diesels' shape. Each electric
  engine is a zone state (same class as a compartment's), a child of compartment
  4's zone state; damage at **`+0x14`**, float32. Starboard = equipment 11 (zone 22
  `ElectricEngine1`); port = equipment 12 (zone 56 `ElectricEngine2`). Verdict
  **authoritative**, both engines, three runs.
- **Chain: the sibling of your diesel pair.** The unit `[[0x5543DC]+4]` has a
  diesel engine block at `+0x300` (its `+0xC` is your `+0x30C` pair) and an
  electric block at `+0x370`, whose `+0xC` is **`+0x37C`**:
  port `[[[[0x5543DC]+0x4]+0x37C]+0x0]`, starboard `…+0x4]`. Each is a pointer to
  the field itself. Array order is [port, starboard]. AOBs (all unique): root
  `89 35 DC 43 55 00 57 8D 7E 10` at `0x004A28B6`; offset
  `8B 80 7C 03 00 00 8D 77 FC` at `0x004C14D1`; zone → equipment byte table
  `0x00566960` via `0F BE B8 60 69 56 00 83 FF FF` at `0x004360EF`.
- **`+0x358`/`+0x360` ruled out:** float64s of the diesel block, constant all run
  and unmoved by electric damage; reading: diesel fuel (capacity, remaining). The
  electric block's matching `+0x398/+0x3A0` is the battery.
- **A mirror to avoid:** the exe keeps a float[44] by equipment index at
  `0x005FD860` (stb electric = `0x005FD88C`), copied from the field once a
  game-second by the crew report (`0x0043620C`); a write to it reverts within a
  second and does nothing. (Your 2026-08-18 pair at `0x240C73E0` was a heap
  address, so it isn't this array; what it was wasn't checked here.)
- **Range: 0.0 healthy → 1.0 destroyed.**
- **Repair threshold: strictly under 1.0.** 0.99999994 (the largest float32 under
  1.0) was repaired 8 of 8 times, both engines; 1.0 held 8 of 8, and for 600+
  game-s under a write watch. Static: the repair gate `0x0042A2D0` runs only for
  `0 < d < 1.0`, with crew strength and at least 2 crew in the room;
  `Destructible` isn't read. Base refit not tested.
- **Repair rate:** 0.00234 damage/game-s (`factor × strength × 0.2/60`, the pump's
  formula), the same at every level: ~427 game-s per 1.0 of damage.
- **Consequences: speed, not battery or noise.** Both engines, ahead flank:
  0 → 3.91 m/s, 0.25 → 2.87, 0.5 → 1.82, 0.75 → 0.81, 0.9 → 0.25, 1.0 coasts.
  Power is scaled by `1 − max(d, clamp(2·(room flood − 0.2), 0, 1))`. Battery drain
  *falls* with damage. The noise property is a class constant.
- **Messages and F7:** "… damaged, sir!" for 0 < d < 0.99, "… destroyed, sir!"
  from 0.99 (still repaired below 1.0). F7's cutaway is orange 0.25–0.9, red by
  0.98. **Ceiling advice: under 0.99, at or under 0.9 to keep F7 out of red**
  (ticket 12's proposal: 0.9).
- **Walkable equipment table: yes, but not a flat damage array** — the zone-state
  tree (compartment list `0x005547D4` → child array `+0x8`, count `+0xC`) with the
  zone → equipment table `0x00566960`. Bears on the map's fog about extending wear
  past the electric engines.

Full write-ups, in the sh3-crush-pressure repo (`d:\code\sh3-crush-pressure`, branch
`automated-depth-re` until merged): `docs/re-notes/electric-engines.md` (§§1–10 the
value, §§11–16 threshold and consequences); finding records
`docs/findings/electric-engine.json` and `docs/findings/repair-threshold.json`.

## Answer (2026-11-25, OAE lab, sha256 `20360fe8…`)

Ported in one session with tickets 10 and 17: `docs/re-notes/08-oae-port-of-depth-leads.md`
§§1–2 (static), §7.2 (live).

- **The value: one float32 per engine, at the engine's zone state `+0x14`**, 0.0
  healthy → 1.0 destroyed. Chain: port `[[[[0x5543DC]+0x4]+0x37C]+0x0]`, starboard
  `…+0x4]`, each a pointer to the field. It sits beside the diesel pair: the diesel
  block is at `+0x300`, the electric block at `+0x370`. AOBs, unique on OAE at vanilla's
  VAs: root `89 35 DC 43 55 00 57 8D 7E 10` @`0x004A28B6`; offset
  `8B 80 7C 03 00 00 8D 77 FC` @`0x004C14D1`. **`+0x358`/`+0x360` is ruled out** (diesel
  fuel doubles, vanilla lead). Avoid the crew report's mirror float[44] at `0x005FD860`.
- **Writes hold and show:** F7 draws damage icons on the electric-engine station.
- **Repair threshold: strictly under 1.0.** On OAE, 0.99999994 was repaired; the gate
  `0x0042A2D0` (`0 < d < 1.0`, ≥ 2 crew) is byte-identical to vanilla.
- **Hard ceiling: 0.9**, as ticket 12's grilling fixed. Margin 0.1 to the threshold
  and 0.09 to the "destroyed" message at 0.99.
- **New on OAE: repair is 60× slower.** h.sie's Realistic Repair Times (flag 1, on;
  `hsie.ini Val1=60.0`) caves the repair step at `0x0041DFB6` and divides it by 60.
  Measured: 5.43e-5 and 5.37e-5 damage/game-s at crew strength 1.0 (predicted 5.56e-5),
  ~4.6 game-hours from the 0.9 ceiling back to 0. The crew repairs one engine at a
  time. This changes ticket 12's tail analysis (see the pointer there).
- **Consequences, from the vanilla lead and unchanged code:** speed, not battery or
  noise. Power is scaled by `1 − max(d, clamp(2·(room flood − 0.2), 0, 1))`. OAE's mod
  loader hooks the propulsion setup (`eng_erpm`, `eng_throttle`), which may change top
  speed but not that factor's read (`SH3Sim+0xA017`, byte-identical).
- **Walkable table:** yes, the zone-state tree; it bears on the map's fog about
  extending wear past the electric engines.
