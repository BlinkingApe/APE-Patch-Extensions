# Black-box proof: do APE's depth effects take effect in play?

Type: task
Status: resolved
Blocked by: 08, 09, 10, 11, 12

## Parent

.scratch/depth-pressure-stress/map.md

## Question

Before the PRD can claim the model is buildable, prove — in play, by observation —
that each of the three effects is genuinely APE's to control. This is the direct
analogue of umbrella ticket 12, which proved the diesel levers by forcing values to
absurd settings and watching an outcome stock could not produce.

The design of that test is the design of this one: **choose settings whose outcome is
unreachable by stock**, so a positive result cannot be coincidence.

Three claims to prove:

1. **Multiplier.** With a large multiplier set, one depth charge at tier-2 depth must
   remove visibly more hull than the same class of hit at 50m. The falsifiable version:
   record HP deltas shallow and deep, and show the deep one exceeds anything stock
   produces for that weapon.
2. **Seep.** Sitting at tier-1 depth with no enemy present must accumulate water in the
   stern compartment. Nothing in stock floods an undamaged, unattacked boat, so **any**
   water at all is proof.
3. **Wear.** With the wear chance forced near-certain, electric-engine damage must
   appear from depth alone, must stop at the hard ceiling however long the boat stays
   deep, and must be repairable by the crew from that ceiling.

Also confirm the two boundary behaviours the model rests on:

- Ascending above tier 1 **stops** seep accrual and wear, and does not remove water
  already taken on.
- Effects **continue** below crush depth rather than switching off — the deliberate
  choice that lets APE never read CDT. Worth one observation to confirm nothing in the
  engine's crush handling interferes.

⚠ Hash the running `sh3.exe` first, and note the install's state before and after —
umbrella ticket 12 left OAE 1.5.2 in test state and the map still carries the warning.
Revert from backups when done.

## Acceptance criteria

- [ ] All three effects observed in play at settings stock cannot produce.
- [ ] The hard ceiling observed to hold under prolonged deep running.
- [ ] Repair from the ceiling confirmed at sea.
- [ ] Both boundary behaviours confirmed.
- [ ] The install reverted to a clean baseline, and said so explicitly in the answer.

## Pointer (2026-11-25): an OAE lab exists, with two traps

The sh3-crush-pressure harness can now run against a disposable copy of the OAE
install (`SH3_LAB=oae`, branch `oae-port`; `docs/re-notes/08-oae-port-of-depth-leads.md`
§7). Two traps for this ticket:

- **Every OAE single mission spawned a Type IIA** (stock Barham's VIIC too). Cause not
  found. A proof meant for a VIIC must check the class first.
- **Attach Frida at the main menu**: once a mission loads, the 32-bit game has no room
  left for it.

## Answer

**Scope narrowed by the owner (2026-11-25).** This ticket was written before tickets
`09`, `10`, `11` and `17` resolved. Those already proved that each write target holds and
shows on OAE: the hull write sticks, stern water floods and is pumped, and engine damage
shows in F7 and is repaired from 0.99999994. What was left fell into two kinds:

- **Claims true by construction of `ape.act`'s own code**: the 0.9 ceiling, effects stop
  above tier 1, effects continue below crush depth, and the multiplier against a real
  depth charge. A stand-in could only prove itself, and `ape.act` is a stub. These become
  **acceptance tests in the PRD**, run against the built `ape.act` (list below).
- **One engine-side gap**: do the writes still hold *below crush depth*, while stock's
  crush writer runs every frame? That was settled live here.

**Live run, OAE lab** (`docs/re-notes/08-oae-port-of-depth-leads.md` §8). The lab and
owner `sh3.exe` both hashed `20360fe8…` before the run. A Frida APE stand-in ran on a Type
IIA (the IIA substitution again) at 200 m, ~26 m below its crush onset, with stock crush
damage running throughout. It used settings stock can't produce. All three writes held:

- **Hull ×2 amplify on top of crush:** 0.81 HP/game-s against a stock-ramp prediction of
  0.41 (×1.97). None of 59 writes was reverted. With the stand-in off, loss went back to the
  stock prediction (0.86 vs 0.85).
- **Seep into compartment 4** (ticket `19`'s rule on a II): water accumulated at seep minus
  pump (+0.00175/game-s net), and F7 reported flooding. Once seep stopped, only the crew's
  pump removed it.
- **Engine wear:** starboard held at the 0.9 ceiling for 120 game-s. Port wear landed
  as written and showed in F7. The crew repaired port at 5.46e-5/game-s, matching ticket `11`.

**Install state:** every write went to the robocopy lab
(`d:\code\sh3-crush-pressure\installs\OAE 1.5.2`). The game was stopped and the session
window restored `Documents\SH3`. The owner's `C:\games\OneAlex Extended 1.5.2\sh3.exe` was
re-hashed unchanged and the lab ledger is unchanged. **The owner's install was never
touched and is at a clean baseline.** (Umbrella ticket 12's old test-state warning
concerns that earlier effort, not this one.)

**PRD acceptance tests (in play, against the built `ape.act`):**

1. Multiplier: a depth-charge or ramming hit at tier-2 depth removes `mult ×` the stock
   hull delta. Crush damage below crush depth is not multiplied (ADR-0011's ramp-share
   exemption).
2. Seep: an undamaged, unattacked boat at tier-1 depth takes on water in the compartment
   ticket `19` names. Water stops rising, and isn't removed by APE, on ascent above tier 1.
3. Wear: with wear forced near-certain, engine damage appears from depth alone, never
   exceeds 0.9 however long the boat stays deep, and is repaired by the crew from 0.9.
   It stops on ascent above tier 1.
4. All three keep acting below crush depth.
5. Save-load: loading a damaged boat costs no amplified HP (the map's two guards).

Acceptance criteria as narrowed: effects observed at settings stock can't produce ✓
(write level, stand-in). Ceiling held under prolonged deep running ✓ (write level;
APE's clamp is a PRD test). Repair at sea ✓ (from 0.6 here, from 0.99999994 in ticket
`11`; repair from exactly 0.9 is a PRD test). Boundary behaviours: below-crush ✓,
tier-1 cut-off → PRD test. Install clean ✓.
