# Crush-time patch: write each U-boat's CrashSpeed from one player-chosen crush time

Type: task
Status: claimed
Blocked by: 18

## Parent

.scratch/depth-pressure-stress/map.md — decided in grilling 2026-11-25, recorded in
ADR-0010.

## What to build

A new GUI patcher patch, separate from the tier fields and shippable **before `ape.act`**
(it is a static edit). The player enters a **crush time** in seconds. For every playable
U-boat class the patcher writes:

    CrashSpeed = Hit_Points ÷ crush time

into that class's `.zon`, using that class's own `Hit_Points` read from the same file.
`CrashSpeed` is hull HP per second (the owner's in-game testing, run under the JSGME test
mod: VIIC, 500 HP, 0.8 → ~625 s).

- **GUI field:** label "Crush time (seconds)", range 1–3600, default **5** for every
  U-boat (owner, 2026-11-25). The hull multiplier never applies to crush damage
  (ADR-0010).
- **Which files:** found at patch time, not from a fixed list. Rule (ticket 18,
  `docs/re-notes/07-playable-uboat-zon-discovery.md`): every `data/Submarine/<X>/<X>.cfg`
  whose `[Unit]` has `HumanPlayable=YES`. Match names case-insensitively. Write
  `<X>/<X>.zon`. That gives 8 classes in both OAE and KC: 2A, 2D, 7b, 7c, 9b, 9c, 9d2, 21.
  The review panel names every `.zon` it will write. AI U-boats of a patched class
  change too, since they share the file.
- **New site kind: `.zon`.** Binary file, keyed float32. In OAE and KC each record is a
  4-byte length, the key, a NUL byte and a little-endian float32. The records sit after
  the *second* `ColisionableObject\0` (ticket 18). Search for `Hit_Points` and
  `CrashSpeed`, which each occur once; don't anchor on `ArmorLevel`, which repeats. Don't
  read the unit `.cfg`'s `[Properties] CrashDepth=`, which is a different value. Verify the key and block before writing,
  as for other sites. Don't trust a fixed offset across bundles.
- **Revert:** on its first write, keep a pristine copy `<name>.zon.ape-orig` beside each
  file. Unchecking restores from it and then deletes it. This is a narrow exception to
  umbrella ticket 02's no-sidecar rule (ADR-0010).
- **Drift:** if a `.zon` no longer holds the value APE wrote (another mod replaced it),
  show that class as "changed by another mod" and leave it alone, as `hsie.ini` sites do.
- **Takes effect at the next mission load.** If `CrashSpeed` is cached at load (re-note
  01 §3), say so in the field's tooltip.

## Settled after ticket 18 (owner, 2026-11-25)

- **Default crush time is 5 s for every U-boat.** It doesn't try to match any install's
  current values. Ticket 18 found that live OAE's 0.8 `CrashSpeed`s came from the owner's
  JSGME test mod "[TEST] - Sub Crush Speed Modded". OAE stock is 0.03–0.1 and KC is 2.0.
- **No JSGME conflict.** This patch replaces the test mod, which the owner removes, so
  APE writes over the bundle's own `.zon` files and `.ape-orig` keeps those. The drift
  check still covers any other mod that later replaces a file.

## Acceptance criteria

- [x] Field present with the range, default and label above.
- [x] Each discovered U-boat `.zon` gets `Hit_Points ÷ crush time`, verified by reading it back.
- [x] Unchecking restores every `.zon` byte-for-byte from its `.ape-orig` copy.
- [x] A `.zon` replaced by another mod shows as such and isn't overwritten.
- [x] Tests in `tests/patcher_gui/` for the `.zon` read, write, revert and drift paths.
- [ ] In play: with crush time set to a short value, a VIIC and a IIA (or IID) at full hull below
      crush depth both die in about that time.

## Answer (built 2026-11-25; the in-play criterion is still open)

Built in the GUI patcher, uncommitted:

- `src/patcher_gui/ZonFile.cs` (new). It discovers classes by ticket 18's rule
  (`[Unit] HumanPlayable=YES`, case-insensitive file names, comments ignored). It parses
  `Hit_Points` and `CrashSpeed` by key: each must occur exactly once, after the second
  `ColisionableObject\0`, with a u32 length prefix of key + 5. It never anchors on
  `ArmorLevel` and never reads the unit `.cfg`'s `CrashDepth`. Each class gets one of
  four states: **Stock** (no `.ape-orig`), **Applied**, **ChangedByOtherMod** and
  **Unreadable**.
- **How drift is judged (a choice made here).** A file counts as APE's only if it
  matches its `.ape-orig` byte-for-byte outside `CrashSpeed`'s 4 bytes, *and* its
  `CrashSpeed` is exactly `float32(Hit_Points ÷ t)` for a whole-second `t` from 1 to
  3600 (or equals the original). Anything else is "changed by another mod" and is never
  written or reverted. The crush time is therefore a whole number of seconds, which is
  also what lets it be read back exactly.
- `PatchSession`: `.zon` writes come after the three existing stores. On APE's first
  write to a class, its `.ape-orig` is written before the `.zon`. Each `.zon` and
  `.ape-orig` is re-checked just before writing: if one changed after APE opened the
  install, the whole apply is refused. A write failure puts back every file this attempt
  wrote, in every store. Copies are deleted only after every write succeeds. The
  feature is unavailable when no class can be written; existing installs and fixtures
  without `data/Submarine` behave as before.
- `PatchState`: `CrushApplied`, `CrushTime` (int, 1–3600, default 5).
- `Review`: a "Crush time" group. It names every `.zon` it will write or restore, gives
  each class's `HP ÷ t = CS` line, calls out that AI boats sharing a file change too,
  warns about each file it leaves alone, and says it takes effect from the next mission
  load. The cache-at-load fact is from re-note 01 §3.
- `ui/MainForm.cs`: a third section with the "Crush time (seconds)" field and a tooltip
  saying it takes effect at the next mission load. The window is 60 px taller.
- `tests/patcher_gui/CrushTimeTests.cs`: 17 cases covering discovery, write, read-back,
  the pristine copy, revert, drift (whole file and CrashSpeed-only), unreadable files,
  change-after-open, range, rollback on write failure, round-trip arithmetic and the
  review text. The suite has 178 cases, all passing in both cultures.
- Read-only check against both real installs: the same 8 classes as re-note 07, all
  Stock, with HP and current CS matching its tables (OAE 0.8, KC 2). At 5 s that gives
  VIIC 100, IIA/IID 80, IXB/IXD2 110, XXI 120, and IXC 110 in OAE, 120 in KC.

Not done here:
- **In play (owner):** with crush time set short, take a VIIC and a IIA or IID below
  crush depth at full hull. Both should die in about that time. Remove the JSGME test mod
  first, so APE's `.ape-orig` holds OAE's own files.
- `tests/patcher_gui/run.cmd` and `build.cmd` currently fail their manifest guard on
  this checkout. The cause is not this ticket: `core.autocrlf=true` checks
  `ape-manifest.json` out with CRLF, while the generator emits LF. The content is
  identical. Fixed by a new `.gitattributes` rule, `data/manifest/*.json text eol=lf`.
