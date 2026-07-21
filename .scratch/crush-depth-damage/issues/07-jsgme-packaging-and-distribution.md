# JSGME packaging + distribution docs

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

A JSGME-installable mod folder containing `ape.act`, a commented `data/Cfg/ape.cfg`, and the per-unit `CrashSpeed`=0 data edits, plus the packaged patcher tool, per ADR-0005. Documentation covers config keys (meaning, units, valid ranges) via comments in the shipped `ape.cfg`, working toward the PDF documentation noted in the spec.

**Neutralization edits (post-pivot):** APE owns 100% of crush-depth damage, so the package must set `CrashSpeed`=0 for each *playable* u-boat class in the data file(s) that hold it (identified by issue 01). These edits ride in the same JSGME folder so enabling APE both loads ape.act and zeroes stock bleed in one step. JSGME load order must let APE's edits overlay whatever OAE/other mods set — document the required ordering.

## Acceptance criteria

- [ ] A JSGME-installable folder is produced containing `ape.act`, `data/Cfg/ape.cfg`, and the per-unit `CrashSpeed`=0 data edits in the correct relative layout, matching the SH3 install structure.
- [ ] The `CrashSpeed`=0 edit is present for each playable u-boat class covered, and the required JSGME load order (APE overlays other mods) is documented.
- [ ] The shipped `ape.cfg` includes comments describing each key's meaning, units (HP per game-second for rates), valid ranges, and the pause/time-compression behavior for the `[CrushDepthDamage]` section.
- [ ] The patcher tool (06-sh3exe-patcher-tool) is packaged alongside for distribution.
- [ ] End-to-end verification: install the JSGME package into a fresh OAE copy, run the patcher, launch the game, and confirm the loop applies banded damage per the debug log while stock `CrashSpeed` bleed is gone.
- [ ] Versioning follows the agreed scheme (`v0.1 → v0.1.1 → ... → v1.0+`); this first package is tagged accordingly.

## Blocked by

- 05-ape-act-hook-install-and-wiring
- 06-sh3exe-patcher-tool
