# 27: In-play acceptance run

**What to build:** proof against the real game that the built `ape.act`, deployed by
the patcher with the player's values written in, does what the PRD claims. Hash
`sh3.exe` first and record the hash. Run ticket 14's five tests:

1. A depth-charge or ramming hit at tier-2 depth removes `mult ×` the stock hull delta,
   while crush damage below crush depth is not multiplied.
2. An undamaged, unattacked boat at tier-1 depth takes water in the electric engine room
   (compartment 4, ADR-0012). The water stops rising above 100 m and isn't removed by APE.
   Run this on a VII or IX from a career if possible, and check that stock's power cut
   follows the water there.
3. With the room past the flooding threshold and the telegraph ahead, both motors take
   damage, never past 0.9. Ordering stop ends it, as does pumping below the threshold. It
   continues above 100 m while the room is flooded, and the crew repairs from 0.9.
4. All effects keep acting below crush depth.
5. Loading a damaged boat costs no amplified hull.

Also run ticket 16's crush-time smoke test (timings for a VIIC and a IIA). Until the
`sh3.exe` load call site exists (ticket 02), load `ape.act` by the lab method. If test 5
shows guard 2 missing a restore, record that; it triggers the PRD's fallback guard
(hold the baseline for ~2 game-seconds after an identity change) as a follow-up.

**Blocked by:** 22 (The patcher deploys, reads back and removes `ape.act`), 25 (Seepage),
30 (Seepage into the electric engine room), 31 (Motor flooding damage), 32 (Telegraph read
on OAE)

**Status:** ready-for-human

- [ ] The `sh3.exe` hash is recorded, and `ape.act` was deployed by the patcher, not hand-copied
- [ ] Tests 1–5 are each recorded as pass or fail, with the `ape_act.log` excerpt that shows it
- [ ] The VIIC and IIA crush-time timings are recorded against the nominal 1.3·T / 0.3·T (ADR-0011)
- [ ] Any failure is filed as its own ticket in this folder

## Comments

### 2026-11-25: first session (owner, not by the checklist; issues reported as found)

Build: Release `ape.act` at `6ea7e25`, patcher with `LoadCallSiteExists` flipped for the
test (uncommitted). `sh3.exe` hash and `ape_act.log` excerpts not yet recorded here.

- **Seepage (test 2, informal): works.** Leaking starts below 100 m, and the rate rises
  below 150 m (tier 2).
- **Normal running:** trivially pumped at both tiers.
- **Filed:** [28](28-quiet-the-flooding-report.md) (flooding report spam),
  [29](29-seepage-sinks-the-boat-in-silent-running.md) (silent running sinks the boat).
- **Hull below crush depth:** a Type II crushes in about 5 s. The owner calls that good
  enough; no numbers were taken. The owner bets it matches the vanilla tests.
