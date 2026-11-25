# Which `.zon` files are playable U-boats? A discovery rule that works for OAE and KC

Type: research
Status: open

## Parent

.scratch/depth-pressure-stress/map.md — needed by ticket 16 (grilling 2026-11-25).

## Question

The crush-time patch writes `CrashSpeed` into every **playable U-boat** class's `.zon`,
found at patch time rather than from a fixed list. What rule picks those files out of
`data/Submarine/`, in both OAE and KC?

OAE 1.5.2's `data/Submarine/` mixes U-boats (`NSS_Uboat14`, `2A`, `2B`, `2D`, `7b`, `7c`,
`7c41`, `7f`, `9b`, `9b4`, `9c`, `9d2`, `21`, `23`) with Allied/AI submarines (`NSS_Gato`,
`SClass`, `TClass`, `600_Class`, `Scuka`, `BARR_`, `DLF`). A name prefix is an
assumption, not a rule. Find what the game itself uses to mark a class playable (unit
`.cfg`, campaign or menu data), and whether non-playable U-boats such as the XIV belong
in the list.

## Acceptance criteria

- [ ] A rule stated with its evidence, checked against the OAE install and a KC install.
- [ ] The resulting file list for each, with each class's current `Hit_Points` and `CrashSpeed`.
