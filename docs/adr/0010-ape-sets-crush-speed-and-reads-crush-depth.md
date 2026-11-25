# APE sets each U-boat's crush speed and reads crush depth, amending ADR-0008

> **Amended by ADR-0011** (2026-11-25): crush damage is a hull-scaled ramp, not a flat HP per second. Crush depth is the live onset `0.7·E`, crush time is nominal, and the exemption is the ramp share. Read ADR-0011 before relying on this ADR's basis or exemption.

ADR-0008 handed everything below crush depth back to stock: APE "never reads CDT and never
neutralizes `CrashSpeed`", and there was "no data edit left to ship". Grilling on
2026-11-25 (after the sh3-crush-pressure leads on tickets 10–12) reversed both halves on
purpose. This ADR records what changed and why, so nobody "fixes" the code back to
ADR-0008.

## Decided

- **APE sets stock's crush speed, through the U-boats' `.zon` files.** The player enters
  one **crush time** in seconds. For every playable U-boat class APE writes
  `CrashSpeed = Hit_Points ÷ crush time` into that class's `.zon`, so every class dies in
  the same time below crush depth. Tier 3 is still stock's mechanism; APE now chooses
  its rate. The basis is the owner's in-game testing: `CrashSpeed` is hull HP per second
  (a VIIC at 500 HP and 0.8 survives ~625 s, about 10.4 minutes).
- **This is its own patch, a static edit, and can ship before `ape.act`.** It needs no
  runtime code. It is the first `.zon` store in the GUI patcher, alongside `sh3.exe`,
  `hsie.ini` and `en_menu.txt`.
- **A pristine copy per edited `.zon`, as a narrow exception to umbrella ticket 02's "no
  sidecar state".** Every other site carries its original value in the manifest. Original
  `CrashSpeed`s differ per class and per bundle (OAE, KC), and ADR-0009 rules out a
  per-bundle table. So on its first write APE keeps `<name>.zon.ape-orig` beside the
  file, and unchecking restores from it. A `.zon` that another mod has since replaced is
  shown as changed by another mod and left alone, as `hsie.ini` sites already are.
- **`ape.act` reads crush depth and crush speed live** from the boat's own game object,
  through the hull-HP chain. Crush depth is tier 2's floor.
- **Below crush depth, APE's tier effects carry on at tier-2 values. Only crush damage
  goes unmultiplied.** Seepage and electric-engine wear continue. The hull multiplier
  still amplifies other damage, e.g. a depth charge landing while the boat crushes.
  Poll-and-amplify can't attribute causes (ADR-0008), so below crush depth APE treats
  `crush speed × elapsed game-seconds` of each drop as crush damage and multiplies only
  the rest. That keeps the player's crush time exact.

## Considered Options

- **A "crush severity ×N" on each boat's own `CrashSpeed`.** Rejected: the owner wants
  one time for every class, not stock's per-class spread (0.8 vs 2.0 in OAE).
- **Switching the multiplier off entirely below crush depth.** Simpler, but it would leave
  hits taken while crushing unmultiplied, against the owner's rule that only crush damage
  is exempt.
- **Revert from a manifest table of originals, or no revert.** Rejected for the reasons
  above. Unchecking is the patcher's only revert mechanism.

## Consequences

- ADR-0008's map entries "Owning the crush-depth threshold" and "per-unit data edits"
  are narrowed, not deleted: APE still doesn't replace stock's crush mechanism.
- If `CrashSpeed` is cached when a mission loads, as `docs/re-notes/01-crush-depth-damage-signature.md`
  §3 records, a change takes effect from the next mission load, not mid-patrol.
- AI U-boats of a patched class change too, because they share the class's `.zon`.
