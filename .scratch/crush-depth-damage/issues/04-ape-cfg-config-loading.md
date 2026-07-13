# Config loading for ape.cfg [CrushDepthDamage]

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

Parsing of `data/Cfg/ape.cfg` (INI format, per ADR-0004) into the band-configuration struct consumed by the damage-band decision function (03-damage-band-decision-function), reading the `[CrushDepthDamage]` section's window-boundary and damage-rate keys. Loaded once, at DLL load — no hot-reload.

## Acceptance criteria

- [x] Given a real `ape.cfg` file with a populated `[CrushDepthDamage]` section, loading produces a band-configuration struct with the correct boundary and rate values, matching the shape the decision function expects.
- [x] Key names follow the agreed descriptive convention (e.g. `light_damage_window`, `light_damage_rate`, and the medium/heavy equivalents).
- [x] Verified manually (a small harness loading a sample file and printing/asserting parsed values) — per the spec's testing decision, this is explicitly not part of the automated unit-test seam.

## Blocked by

- 03-damage-band-decision-function (shares the band-configuration struct shape). **Unblocked** — 03 is complete (commit `1acf9be`). The struct to populate is `ape::BandConfig` in `src/core/damage_band.hpp`: `light_window`/`medium_window`/`heavy_window` (fractions of crush depth) and `light_rate`/`medium_rate`/`heavy_rate`. Map the config's `light_damage_window` / `light_damage_rate` (etc.) keys onto those fields.

## Comments

**COMPLETE (Windows 11).** Loader implemented as `ape::parse_band_config` (istream) + `ape::load_band_config` (file) in `src/core/config.{hpp,cpp}`, built as the new `ape_config` static lib and linked into `ape_act`. Parsing kept out of `ape_core` so that library keeps its documented "no I/O" contract. Parser: trims whitespace, skips blank lines, supports `;`/`#` whole-line and trailing comments, matches section+keys case-insensitively, last-write-wins on duplicate keys, ignores unknown keys (forward-compatible), and rejects a missing section, any missing required key, or an unparseable/negative number with a human-readable `error` string (leaving `out` untouched on failure). All six keys are required.

Shipped a documented `data/Cfg/ape.cfg` (comments cover each key's meaning, units, valid ranges — PRD user story 18). Verified manually via the `config_check` harness (`tools/config_check/main.cpp`, its own CMake target, deliberately **not** registered with ctest per the spec's testing decision): it self-checks an embedded sample and asserts every field, checks that an incomplete section is rejected, and prints the parsed values of the real shipped `ape.cfg`. Ran it against `data/Cfg/ape.cfg` — all fields correct, self-check PASSED. Full `ctest` seam (`ape_tests`) still green.

Not in scope here / deferred to **05**: actually calling `load_band_config` from `DllMain` and resolving `ape.cfg`'s runtime path relative to the game root — that's part of the live hook wiring.
