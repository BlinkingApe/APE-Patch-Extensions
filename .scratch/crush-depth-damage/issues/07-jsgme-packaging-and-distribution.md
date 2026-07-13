# JSGME packaging + distribution docs

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

A JSGME-installable mod folder containing `ape.act` and a commented `data/Cfg/ape.cfg`, plus the packaged patcher tool, per ADR-0005. Documentation covers config keys (meaning, units, valid ranges) via comments in the shipped `ape.cfg`, working toward the PDF documentation noted in the spec.

## Acceptance criteria

- [ ] A JSGME-installable folder is produced containing `ape.act` and `data/Cfg/ape.cfg` in the correct relative layout, matching the SH3 install structure.
- [ ] The shipped `ape.cfg` includes comments describing each key's meaning, units, and valid ranges for the `[CrushDepthDamage]` section.
- [ ] The patcher tool (06-sh3exe-patcher-tool) is packaged alongside for distribution.
- [ ] End-to-end verification: install the JSGME package into a fresh OAE copy, run the patcher, launch the game, and confirm the hook fires per the debug log.
- [ ] Versioning follows the agreed scheme (`v0.1 → v0.1.1 → ... → v1.0+`); this first package is tagged accordingly.

## Blocked by

- 05-ape-act-hook-install-and-wiring
- 06-sh3exe-patcher-tool
