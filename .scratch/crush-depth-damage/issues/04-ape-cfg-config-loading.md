# Config loading for ape.cfg [CrushDepthDamage]

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

Parsing of `data/Cfg/ape.cfg` (INI format, per ADR-0004) into the band-configuration struct consumed by the damage-band decision function (03-damage-band-decision-function), reading the `[CrushDepthDamage]` section's window-boundary and damage-rate keys. Loaded once, at DLL load — no hot-reload.

## Acceptance criteria

- [ ] Given a real `ape.cfg` file with a populated `[CrushDepthDamage]` section, loading produces a band-configuration struct with the correct boundary and rate values, matching the shape the decision function expects.
- [ ] Key names follow the agreed descriptive convention (e.g. `light_damage_window`, `light_damage_rate`, and the medium/heavy equivalents).
- [ ] Verified manually (a small harness loading a sample file and printing/asserting parsed values) — per the spec's testing decision, this is explicitly not part of the automated unit-test seam.

## Blocked by

- 03-damage-band-decision-function (shares the band-configuration struct shape).
