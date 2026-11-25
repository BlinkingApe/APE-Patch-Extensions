# Black-box proof: do APE's depth effects take effect in play?

Type: task
Status: open
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
