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

**Status:** ready-for-agent

- [ ] Enabling depth stress and applying deploys `ape.act` with the marker checked and the struct bytes matching the entered values after conversion
- [ ] Reopening reads the values back from the deployed file; with no deployed file, from the embedded template's defaults
- [ ] Disabling and applying deletes `ape.act`; with no runtime feature enabled, no `ape.act` is left in the game root
- [ ] A wrong marker or offset refuses the apply and writes nothing
- [ ] A write failure anywhere in the apply rolls back every store, `ape.act` included
- [ ] Ranges and neutral values hold per field (tier-2 depth 101–300, leakage 0–60, wear amount 0–90, wear rate 1–600, multiplier 1.0–5.0); a milder tier 2 than tier 1 is accepted
- [ ] The crush-time patch still applies and reverts on its own, without `ape.act`
- [ ] All tests run in both cultures, as the existing suite does
