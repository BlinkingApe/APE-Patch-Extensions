# 22: The patcher deploys, reads back and removes `ape.act`

**What to build:** the GUI patcher handles `ape.act` as a new store, next to `sh3.exe`,
`hsie.ini`, `en_menu.txt` and `.zon` (ADR-0006). The C++ build's `ape.act` is embedded
in the patcher exe as a template. `PatchState` gains a depth-stress feature flag and the
nine fields, each checked only against its own range (no cross-field validation), with
each effect switched off at its neutral value (leakage 0, wear amount 0, multiplier 1.0).
On apply with depth stress enabled, the patcher copies the template to the game root,
checks the marker at the manifest offset, and writes the struct, doing the unit
conversions there rather than per poll (`p/6000` flood per game-second, `q = 1/(60·m)`,
`a/100` damage). On apply with it disabled, the patcher deletes `ape.act`. Read-back
comes from the deployed file if there is one, otherwise from the embedded template. The
write joins `PatchSession`'s all-or-nothing apply and its rollback.

**Blocked by:** 21 (`ape.act` constants struct, verified loader and test harness)

**Status:** resolved

- [x] Enabling depth stress and applying deploys `ape.act` with the marker checked and the struct bytes matching the entered values after conversion
- [x] Reopening reads the values back from the deployed file; with no deployed file, from the embedded template's defaults
- [x] Disabling and applying deletes `ape.act`; with no runtime feature enabled, no `ape.act` is left in the game root
- [x] A wrong marker or offset refuses the apply and writes nothing
- [x] A write failure anywhere in the apply rolls back every store, `ape.act` included
- [x] Ranges and neutral values hold per field (tier-2 depth 101–300, leakage 0–60, wear amount 0–90, wear rate 1–600, multiplier 1.0–5.0); a milder tier 2 than tier 1 is accepted
- [x] The crush-time patch still applies and reverts on its own, without `ape.act`
- [x] All tests run in both cultures, as the existing suite does

## Comments

**2026-11-25, implemented.**

- `src/patcher_gui/ApeAct.cs` (`DepthStress`) holds the struct logic: it checks the marker
  and version at the manifest's offset, writes the nine fields in runtime units
  (`p/6000`, `1/(60·m)`, `a/100`), and reads them back (rounded to 2 decimals).
  `Manifest.cs` parses the `apeAct` block into `ApeActStruct`.
- `PatchState` gains `DepthApplied` and the nine fields. Each is range-checked on its
  own (`DepthStressValid`), and `Apply` refuses when any is out of range.
- `PatchSession` reads `ape.act` from the game root. Depth stress counts as applied when
  the file exists. Read-back comes from the deployed file, or from the template if the
  deployed file's marker doesn't check out or there is no file.
- `PatchSession` plans a deploy (skipped when the bytes already match) or a delete. A
  template with a wrong marker, version or offset becomes a plan-time refusal, and
  `Apply` refuses before any write.
- `ape.act` is the last store written. Rollback was refactored into one `Undo`/`RollBack`
  shared by the `.zon` and `ape.act` stages. `.zon.ape-orig` deletion for reverted
  classes (`FinishZons`) now runs after every store, `ape.act` included, so a late
  failure never needs a deleted copy back.
- `build.cmd` and `tests/patcher_gui/run.cmd` embed `build/x86-release/ape.act` as the
  `ape.act` resource. Both now **fail without it**, so the template is mandatory and
  `gen_manifest.py --check` always measures the offset from the binary being embedded.
- Tests: `tests/patcher_gui/DepthStressTests.cs`, 18 cases × 2 cultures. Two hand
  mutations were each caught: the leakage conversion, and leaving a partial file behind
  after a failed write.
- The ape.act-only plan has no `Review` group yet. That's ticket 23, as is the UI
  section, so for now nothing in the GUI can switch depth stress on.
