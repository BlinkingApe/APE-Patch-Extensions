# 27: In-play acceptance run

**What to build:** proof against the real game that the built `ape.act`, deployed by
the patcher with the player's values written in, does what the PRD claims. Hash
`sh3.exe` first and record the hash. Run ticket 14's five tests:

1. A depth-charge or ramming hit at tier-2 depth removes `mult ×` the stock hull delta,
   while crush damage below crush depth is not multiplied.
2. An undamaged, unattacked boat at tier-1 depth takes water in ticket 19's
   compartment. The water stops rising above 100 m and isn't removed by APE. Run this
   on a VII or IX from a career if possible.
3. With wear forced near-certain, engine damage appears from depth alone, never exceeds
   0.9, is repaired by the crew from 0.9, and stops above 100 m.
4. All three effects keep acting below crush depth.
5. Loading a damaged boat costs no amplified hull.

Also run ticket 16's crush-time smoke test (timings for a VIIC and a IIA). Until the
`sh3.exe` load call site exists (ticket 02), load `ape.act` by the lab method. If test 5
shows guard 2 missing a restore, record that; it triggers the PRD's fallback guard
(hold the baseline for ~2 game-seconds after an identity change) as a follow-up.

**Blocked by:** 22 (The patcher deploys, reads back and removes `ape.act`), 25 (Seepage),
26 (Electric-engine wear)

**Status:** ready-for-human

- [ ] The `sh3.exe` hash is recorded, and `ape.act` was deployed by the patcher, not hand-copied
- [ ] Tests 1–5 are each recorded as pass or fail, with the `ape_act.log` excerpt that shows it
- [ ] The VIIC and IIA crush-time timings are recorded against the nominal 1.3·T / 0.3·T (ADR-0011)
- [ ] Any failure is filed as its own ticket in this folder
