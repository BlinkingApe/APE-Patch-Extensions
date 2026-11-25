# Live values: crush depth and crush speed from the boat object, and does APE's tier-3 boundary match stock's?

Type: research
Status: open
Blocked by: 09

## Parent

.scratch/depth-pressure-stress/map.md — decided in grilling 2026-11-25 (ADR-0010).

## Question

Tier 2 now ends at crush depth, and below it APE leaves crush damage unmultiplied by
treating `crush speed × elapsed game-seconds` of each hull drop as crush damage. So
`ape.act` needs both values live on OAE.

1. **Crush depth.** The sh3-crush-pressure lead (vanilla 1.4b) puts `CrashDepth` at
   ColisionableObject `+0xC0`, next to hull `+0xB8` and max hull `+0xB4`, through ticket
   `09`'s chain. It recorded the field reading a tenth of the `.zon` value. It also
   recorded crush damage starting at 247 m for a `.zon` `CrashDepth` of 346, and starting
   shallower as hull fell. Confirm the offset on OAE. Then find what depth APE must
   compare against so that **APE's tier 3 starts on the same frame stock's crush damage
   starts**, at full hull and at reduced hull.
2. **Crush speed.** The same lead puts `CrashSpeed` at `+0xC4`. Confirm it on OAE, and
   confirm it holds the value ticket 16 wrote to the `.zon`, from the next mission load.

## Acceptance criteria

- [ ] Both offsets confirmed on a hashed OAE build (playbook: hash first).
- [ ] APE's crush-depth boundary shown to coincide with the onset of stock's crush writer
      (`SH3Collisions+0x30D7`), at full hull and at ~60 % hull.
- [ ] Live crush speed shown to equal the `.zon` value after a mission load.
