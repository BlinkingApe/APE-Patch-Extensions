# Config loading for ape.cfg [CrushDepthDamage]

Status: ready-for-agent (reopened — pivot rework, see Comments)

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

Parsing of `data/Cfg/ape.cfg` (INI format, per ADR-0004) into the band-configuration struct consumed by the damage-band decision function (03-damage-band-decision-function), reading the `[CrushDepthDamage]` section's window-boundary and damage-rate keys. Loaded once, at DLL load — no hot-reload.

Post-pivot the section has **five** keys, not six: two cumulative window boundaries (`light_damage_window`, `medium_damage_window`, fractions of crush depth) and three flat rates (`light_damage_rate`, `medium_damage_rate`, `heavy_damage_rate`, in **HP per game-second**). The heavy band is open-ended, so there is no `heavy_damage_window` key.

## Acceptance criteria

- [x] Given a real `ape.cfg` file with a populated `[CrushDepthDamage]` section, loading produces a band-configuration struct with the correct boundary and rate values, matching the shape the decision function expects.
- [x] Key names follow the agreed descriptive convention (e.g. `light_damage_window`, `light_damage_rate`, and the medium/heavy equivalents).
- [x] Verified manually (a small harness loading a sample file and printing/asserting parsed values) — per the spec's testing decision, this is explicitly not part of the automated unit-test seam.

## Blocked by

- 03-damage-band-decision-function (shares the band-configuration struct shape). After the pivot rework, the struct to populate is `ape::BandConfig` in `src/core/damage_band.hpp`: `light_window`/`medium_window` (fractions of crush depth) and `light_rate`/`medium_rate`/`heavy_rate` (HP per game-second). Map the config's `light_damage_window` / `light_damage_rate` (etc.) keys onto those fields. `heavy_window`/`heavy_damage_window` no longer exists.

## Comments

**COMPLETE (Windows 11).** Loader implemented as `ape::parse_band_config` (istream) + `ape::load_band_config` (file) in `src/core/config.{hpp,cpp}`, built as the new `ape_config` static lib and linked into `ape_act`. Parsing kept out of `ape_core` so that library keeps its documented "no I/O" contract. Parser: trims whitespace, skips blank lines, supports `;`/`#` whole-line and trailing comments, matches section+keys case-insensitively, last-write-wins on duplicate keys, ignores unknown keys (forward-compatible), and rejects a missing section, any missing required key, or an unparseable/negative number with a human-readable `error` string (leaving `out` untouched on failure). All six keys are required.

Shipped a documented `data/Cfg/ape.cfg` (comments cover each key's meaning, units, valid ranges — PRD user story 18). Verified manually via the `config_check` harness (`tools/config_check/main.cpp`, its own CMake target, deliberately **not** registered with ctest per the spec's testing decision): it self-checks an embedded sample and asserts every field, checks that an incomplete section is rejected, and prints the parsed values of the real shipped `ape.cfg`. Ran it against `data/Cfg/ape.cfg` — all fields correct, self-check PASSED. Full `ctest` seam (`ape_tests`) still green.

Not in scope here / deferred to **05**: actually calling `load_band_config` from `DllMain` and resolving `ape.cfg`'s runtime path relative to the game root — that's part of the live wiring.

**REOPENED (2026-07-21, architecture pivot).** The completed loader required six keys including `heavy_damage_window`. Rework needed to track the new band model (issue 03):
- **Drop the `heavy_damage_window` key** and its mapping — the heavy band is open-ended, so `BandConfig` no longer has `heavy_window`. Five required keys remain.
- **Rate keys are HP per game-second** — update the units in the shipped `ape.cfg` comments and the key-doc text (were "damage-per-tick"). Also document the pause / time-compression behavior for tuners (damage accrues in game-time).
- Re-run the `config_check` harness against the updated `ape.cfg`.
