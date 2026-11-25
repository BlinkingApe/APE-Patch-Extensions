# Crush-time patch: write each U-boat's CrashSpeed from one player-chosen crush time

Type: task
Status: resolved
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
- [~] *(Waived by the owner, 2026-11-25, for time.)* In play: with crush time set to a short value, a VIIC and a IIA (or IID) at full hull below
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

## Pointer (2026-11-25): crush rate measured on OAE

The crush rate measured live on OAE wasn't flat. It grew from 0.40 to 1.77
HP/game-s as hull fell, at one depth ([ticket 17](17-live-crush-depth-and-speed.md)'s
answer). That conflicts with this ticket's "`CrashSpeed` is hull HP per second".
The owner has asked that the flat rate be assumed. The question is
[ticket 20](20-crush-rate-ramp-vs-adr-0010.md)'s; nothing here changes.

## Pointer (2026-11-25): ticket 20 resolved, crush time is nominal

[Ticket 20](20-crush-rate-ramp-vs-adr-0010.md) settled on the measured ramp
(ADR-0011). The patch and its formula stand. Two things here change:

- **The GUI tooltip** says "how long a U-boat at full hull lasts below crush depth". It
  should say that T is nominal: just past the onset a boat lasts about 1.3·T, and at its
  rated crush depth about 0.3·T. Deeper or damaged boats die faster. The onset is 70 % of
  the rated crush depth at full hull.
- **The in-play criterion** "both die in about that time" becomes: at full hull, a few
  metres past the onset, both last about 1.3·T; at the rated crush depth, about 0.3·T.
  At T = 5 s that is roughly 2–7 s.

**Done (2026-11-25):** the tooltip (`ui/MainForm.cs`) and the Review's crush-time line
(`Review.cs`) now call the time rough and give the "longer just past, a third at the
rated crush depth" spread. `CrushTimeTests` checks the Review wording. The in-play
criterion above is still open and still to be read as reworded.

## In-play checklist (not run: waived)

The last open criterion. Expected values use OAE's own `.zon` files (the JSGME backup
column of re-note 07), which are what the boats carry once the test mod is gone.

1. **Remove the test mod.** In JSGME, deactivate "[TEST] - Sub Crush Speed Modded".
   Check that `NSS_Uboat7c.zon` and `NSS_Uboat2A.zon` match their `MODS/!BACKUP` copies.
2. **Hash `sh3.exe`** (playbook) and note it. The expected hash is `20360fe8…`.
3. **Patch.** Open the GUI patcher, tick Crush time, and set **T = 60 s**. 60 s is still
   short next to stock's thousands of seconds, but it gives timings a stopwatch can
   resolve; 5 s would give 1.5–6.5 s. The review should list 8 `.zon` files with VIIC
   `500 ÷ 60 = 8.33` and IIA `400 ÷ 60 = 6.67`. Apply, then confirm a `.ape-orig`
   sits beside each file.
4. **VIIC, just past the onset.** Rated 350 m, so the onset is 245 m reference, about
   248–250 m on the gauge. Load a fresh mission (crush speed is cached at load), take the
   boat down at full hull to about **255 m**, hold depth, and time it from the first hull
   loss to the sinking. **Expect about 1.3·T, roughly 60–90 s.**
5. **VIIC at its rated crush depth.** Repeat at full hull, going to **350 m** as fast as
   the boat allows. Time it from reaching 350 m. **Expect about 0.3·T, roughly 15–25 s.**
6. **IIA or IID, the same two runs.** Rated 230 m, so the onset is about 164–166 m on the
   gauge. Run at **~170 m**, then at **230 m**. Same expectations.
7. **Revert.** Untick Crush time and apply. Each `.zon` should match its `!BACKUP` copy
   byte for byte, and the `.ape-orig` files should be gone.

Report the four times (and the hash) here. Pass: each time sits in its band. A time
an order of magnitude off would mean the patch isn't reaching the game (wrong file, or
a mod still overriding it).

## Resolution (2026-11-25)

**Resolved, with the in-play criterion waived by the owner for time.** The patch is
built and committed (`ccb8bd5`, tooltip reworded in `c271aa6`). It is covered by 17
`CrushTimeTests` cases and checked read-only against both installs. Nothing proves in
play that the written `CrashSpeed` gives the nominal crush time. What stands in for it:
ticket 17 showed live `+0xC4` equals the `.zon` `CrashSpeed` after load, and ticket 20
fitted stock's ramp to that value within 5 %. The unrun checklist above stays as the
smoke test, for a release check or for when a player reports crush times that are off.
