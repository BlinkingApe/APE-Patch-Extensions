# Which `.zon` files are playable U-boats? A discovery rule that works for OAE and KC

Type: research
Status: resolved

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

- [x] A rule stated with its evidence, checked against the OAE install and a KC install.
- [x] The resulting file list for each, with each class's current `Hit_Points` and `CrashSpeed`.

## Answer

Resolved 2026-11-25 by a static pass over both installs. Detail, evidence and per-class
tables: [`docs/re-notes/07-playable-uboat-zon-discovery.md`](../../../docs/re-notes/07-playable-uboat-zon-discovery.md).

**Rule:** a class is a playable U-boat when `data/Submarine/<X>/<X>.cfg` has
`HumanPlayable=YES` in its `[Unit]` section. Its file is `data/Submarine/<X>/<X>.zon`.
Match names case-insensitively (KC has `NSS_Uboat21.CFG`). A missing key or `NO` means
not playable. Evidence: `simdata.dll` reads the key, `sh3.exe`'s built-in player-class
list matches the same 8 classes, `Basic.cfg`/`Flotilla.cfg` agree, and OAE's
`EnglishNames.cfg` comments them as `;Human Playable`. `UnitType` (200 for every sub) and
the `NSS_Uboat` prefix both fail as rules.

**Both installs give the same 8:** `NSS_Uboat2A`, `2D`, `7b`, `7c`, `9b`, `9c`, `9d2`,
`21`. VIIC/41, VIIC/42 and IXC/40 share the `7c`/`9c` files (inferred from what KC mods
replace, not traced in the loader). **XIV and the other AI U-boats (14, 2B, 7c41, 7f,
9b4, 23) are excluded.** KC's `NSS_Undine` is the British U-class (Allied AI). `HBW` is a
whale (no `.zon`).

**`.zon` layout, same across all 42 files in both installs:** 4-byte length (15), key,
NUL, little-endian float32. These records come after the *second* `ColisionableObject\0`,
in the order ArmorLevel, Hit_Points, CrashDepth, CrashSpeed, Rebound. `Hit_Points`,
`CrashDepth` and `CrashSpeed` each appear once per file. `ArmorLevel` appears many
times, so it is no anchor. Offsets vary per file, so the patcher must search for each key.

**Current crush times (Hit_Points ÷ CrashSpeed):** KC is 200–300 s (CrashSpeed 2.0 for
all). Live OAE is 500–750 s (CrashSpeed 0.8), **but only because the owner's JSGME test
mod "[TEST] - Sub Crush Speed Modded" is enabled.** OAE stock (from `MODS/!BACKUP`) has
CrashSpeed 0.03–0.1, e.g. the VIIC at 0.08, which is 6250 s.

**Consequences for ticket 16**, recorded there: the 625 s default reflects the test mod,
not stock. JSGME can restore files over APE's writes, so `.ape-orig` may capture
test-mod bytes. The in-game check named a IIB, which isn't playable. And the unit
`.cfg`'s `[Properties] CrashDepth=` is a different value from the `.zon`'s crush depth.
