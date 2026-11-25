# 31: Motor flooding damage

**What to build:** replace ticket 26's random electric-engine wear with motor flooding
damage (ADR-0012, PRD step 7). At any depth, in a tier or not, with amount > 0 and
dt > 0: if the electric engine room's flood (read this poll, before APE's seepage write)
is at or above the threshold, and the ordered telegraph is not 0.0, **both** engines
step by `amount·dt`. Each is clamped at 0.9, gets no write at or above 0.9, and is never
lowered. Astern counts as running. With no telegraph value there is no motor damage this
poll; the other effects still run.

- **`ape.act`.** The reader adds the telegraph at `[0x0063E1D4]` (record an AOB per
  ADR-0003, preferring the instruction in h.sie's diesel cave that writes it) with a
  plausibility gate. The step drops the generator input, `Mt19937Random` and the
  per-tier wear constants. Logging shows flood, threshold, telegraph and each engine's
  old → new.
- **Constants struct.** The per-tier wear amount and wear rate go. Add one flooding
  threshold and one motor damage amount (float32). Bump the struct version.
- **Patcher.** The four wear fields go. Add **Flooding threshold** (% flood, 5–95,
  default 30) and **Motor damage** (% of each motor's health per game-second, 0–100,
  default 10, 0 = off). Both defaults are provisional, pending Stoss. `PatchState`,
  read-back, the UI section, tooltips ("ordering stop protects the motors") and Review
  are updated to match.

**Blocked by:** 30 (Seepage into the electric engine room)

**Status:** resolved (code; telegraph proven live by ticket 32, in-play by ticket 27)

- [x] Step tests: both engines step by `amount·dt`; clamp at 0.9 (allowing for 0.89999998); no write at or above 0.9; never lowers
- [x] Step tests: nothing below the threshold, at telegraph 0.0, with no telegraph value, at dt ≤ 0, or at amount 0; acts above 100 m and outside any tier when flooded; astern counts as running
- [x] Step tests: the trigger uses the flood read this poll, not the value after APE's seepage write
- [x] No generator remains in the step or the loop
- [x] Constants-struct version bumped; marker/offset tests and `PatchSession` round-trip updated; Review and tooltips covered in both cultures
- [x] The telegraph's AOB is unique on OAE and written up in `docs/re-notes/` with the build hash

## Notes

Ticket 32 proves the telegraph read live. This ticket's code can be built before 32
lands. If 32 finds that `[0x63E1D4]` doesn't track while submerged on electric, the
reader's source changes, not the step.

## Comments

2026-11-25, implemented.

- **Step.** `Sample` gained `telegraph`/`has_telegraph`; `step()` lost its `Random*`, and
  `Random`/`Mt19937Random` and `<random>` are gone from the step and the loop. Motor
  damage is step 7, after seepage, and tests `s.seep_flood` (the value read this poll).
  "Running" is `has_telegraph && telegraph != 0.0f`, so -0.0 is stop and any negative
  (astern) runs. `kWearCeiling` is now `kMotorCeiling`, still 0.9. Motor damage also needs
  the motor-room flood (`has_seep`): no compartment-4 zone means no motor damage.
- **Telegraph gate.** `depth_stress::telegraph_plausible`: finite and in [-1, 1], tested
  at seam 1. The reader applies it; a failure is "no telegraph value", not "no sample",
  and logs `no motor damage: <why>` when the reason changes. If ticket 32 finds astern
  encoded outside [-1, 1], the gate widens.
- **AOB.** `8B 80 20 03 00 00 8B 00 89 05 ?? ?? ?? ??`, operand at +10, the cave's store
  at `0x00635065`. Unique in OAE 1.5.2 (`20360fe8`), OAE 2.0 TEST_2/TEST_3, GWC-KC 2.5
  and `sh3_vanilla.exe`. Note 04's "`a3 ->`" is shorthand: the store is `89 05`.
  Written up in `docs/re-notes/09-depth-stress-runtime-anchors.md`.
- **Struct v2, 40 bytes.** `tier2_depth_m` 12, `flood_threshold` 16 (fraction),
  `motor_damage_per_s` 20 (fraction/s), then per tier `seep_per_s`, `hull_mult` from 24.
  `gen_manifest.py` and the regenerated manifest match.
- **Patcher.** `FloodThreshold` (5-95, default 30) and `MotorDamage` (0-100, default 10)
  replace the four wear fields; UI section "Motor flooding — at any depth". Review prints
  one motor line, plus "Ordering stop protects the motors" when it is on. The old Review
  guard against "stern" now checks " stern", since "astern" is rightly in the motor line.
- **Tests.** doctest 48/48. C# suite 230/232 in both cultures; the two failures are
  `TheDepthStressSectionIsGatedByOneSwitchAndExplainsWhy`, caused by the separate,
  uncommitted `LoadCallSiteExists = true` edit (232/232 with it at HEAD's `false`).
