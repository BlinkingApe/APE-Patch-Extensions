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
`CrashSpeed` is hull HP per second (the owner's in-game testing: VIIC, 500 HP, 0.8 →
~625 s).

- **GUI field:** label "Crush time (seconds)", range 1–3600, default **625**, so a first
  run leaves a VIIC unchanged. The hull multiplier never applies to crush damage
  (ADR-0010).
- **Which files:** found at patch time, not from a fixed list (the rule comes from ticket
  18). The review panel names every `.zon` it will write. AI U-boats of a patched class
  change too, since they share the file.
- **New site kind: `.zon`.** Binary file, keyed float32. In OAE each value follows its key
  name inside the `ColisionableObject` block (`Hit_Points`, `CrashDepth`, `CrashSpeed`,
  then a NUL byte and a little-endian float32). Verify the key and block before writing,
  as for other sites. Don't trust a fixed offset across bundles.
- **Revert:** on its first write, keep a pristine copy `<name>.zon.ape-orig` beside each
  file. Unchecking restores from it and then deletes it. This is a narrow exception to
  umbrella ticket 02's no-sidecar rule (ADR-0010).
- **Drift:** if a `.zon` no longer holds the value APE wrote (another mod replaced it),
  show that class as "changed by another mod" and leave it alone, as `hsie.ini` sites do.
- **Takes effect at the next mission load.** If `CrashSpeed` is cached at load (re-note
  01 §3), say so in the field's tooltip.

## Acceptance criteria

- [ ] Field present with the range, default and label above.
- [ ] Each discovered U-boat `.zon` gets `Hit_Points ÷ crush time`, verified by reading it back.
- [ ] Unchecking restores every `.zon` byte-for-byte from its `.ape-orig` copy.
- [ ] A `.zon` replaced by another mod shows as such and isn't overwritten.
- [ ] Tests in `tests/patcher_gui/` for the `.zon` read, write, revert and drift paths.
- [ ] In play: with crush time set to a short value, a VIIC and a IIB at full hull below
      crush depth both die in about that time.
