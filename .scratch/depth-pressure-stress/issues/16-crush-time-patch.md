# Crush-time patch: write each U-boat's CrashSpeed from one player-chosen crush time

Type: task
Status: open
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

- [ ] Field present with the range, default and label above.
- [ ] Each discovered U-boat `.zon` gets `Hit_Points ÷ crush time`, verified by reading it back.
- [ ] Unchecking restores every `.zon` byte-for-byte from its `.ape-orig` copy.
- [ ] A `.zon` replaced by another mod shows as such and isn't overwritten.
- [ ] Tests in `tests/patcher_gui/` for the `.zon` read, write, revert and drift paths.
- [ ] In play: with crush time set to a short value, a VIIC and a IIA (or IID) at full hull below
      crush depth both die in about that time.
