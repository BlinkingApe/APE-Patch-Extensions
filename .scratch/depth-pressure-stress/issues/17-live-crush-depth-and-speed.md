# Live values: crush depth and crush speed from the boat object, and does APE's tier-3 boundary match stock's?

Type: research
Status: resolved
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

- [x] Both offsets confirmed on a hashed OAE build (playbook: hash first).
- [ ] APE's crush-depth boundary shown to coincide with the onset of stock's crush writer
      (`SH3Collisions+0x30D7`), at full hull and at ~60 % hull.
- [x] Live crush speed shown to equal the `.zon` value after a mission load.

## Answer (2026-11-25, OAE lab, sha256 `20360fe8…`)

Research ticket, answered with what was found. The conflict below is ticket 20's to
decide. `docs/re-notes/08-oae-port-of-depth-leads.md` §5 (static), §7.3 (live). The
live boat was a IIA (note §7: every OAE single mission spawned one).

- **Crush depth: `+0xC0` = `.zon` CrashDepth ÷ 10.** Live 25.1 against the IIA's 251. The
  crush function `SH3Collisions+0x2E70…+0x30FD` is byte-identical to vanilla.
- **Crush speed: `+0xC4` = the `.zon` CrashSpeed after the mission load.** Live 0.8
  against 0.8 (the owner's test-mod value; ticket 16's patch isn't applied in the lab).
- **Where stock's crush damage starts: at `0.7 · E`, not at crush depth**, with
  `E = 1 + (CrashDepth_field − 1) · hull/max` (×10 for metres). It is measured at a hull
  reference point, not the gauge depth APE reads: 2.6 m above the gauge on the IIA,
  ~5 m on vanilla's VIIC. APE can compute this from `+0xB4`, `+0xB8` and `+0xC0` every
  poll. Not seen frame by frame: the three-window fit below puts the onset there.
- **Crush loss isn't flat on OAE.** At a fixed 200.0 m it was 0.40, 0.83 and 1.77
  HP/game-s at 0.92, 0.78 and 0.55 of max hull. The code's
  `CrashSpeed · dt · (D − 0.7E)/(0.3E)` predicts all three within 5 % from one fitted
  constant; a flat 0.8 fits none. **This contradicts the owner's in-game experience
  and ADR-0010's basis.** The owner has asked that the flat rate be assumed. Ticket 20
  holds the conflict; nothing here re-decides it.

Criteria: both offsets ✓. The boundary is shown at `0.7·E` across 0.55–0.92 hull by
fit, not by a first frame of onset (partial). Live speed = `.zon` ✓.
