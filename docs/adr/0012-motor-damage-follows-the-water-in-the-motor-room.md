# Motor damage follows the water in the motor room, and seepage goes there on every class

Ticket 26 built electric-engine wear as random hits at depth: per tier, a fixed amount
per hit at a mean interval, one motor picked at random. Feedback from Stoss (2026-11-25)
asked for engine damage keyed to the flooding in the motors' compartment instead. The
crew pumps to keep it in check, and past some level the motors must be stopped or they
take damage. Grilling the same day adopted that and moved seepage to make it coherent.
This ADR records why a depth-stress feature damages motors at 50 m.

## Decided

- **Seepage goes into compartment 4, the electric engine room, on every U-boat.** It no
  longer goes into compartment 8 for VII and IX (ticket 10) or "8, else 4" (ticket 19).
  The motors sit in compartment 4 on every class (OAE `Basic.cfg`,
  `Comp4=4; ElectricEngine` holding port and starboard), so water and motors share one
  room. `ape.act` still picks it at run time from the zone→compartment table, never by
  class.
- **Motor flooding damage replaces electric-engine wear.** While compartment 4's flood is
  at or above the player's threshold **and** the ordered machine telegraph is not at stop,
  both motors take the player's damage amount per game-second, continuously and
  deterministically. Each is capped at 0.9 (unchanged rule: no write at or above 0.9,
  else `min(d + amount·dt, 0.9)`, never lowered). There is no randomness, so the 32-bit
  generator constraint goes too.
- **It acts at any depth, whatever flooded the room**, including stock's own breach
  flooding. It is keyed to water, not depth, and APE's water stays after ascent.
- **One threshold and one amount, not per tier.** Depth drives it through the per-tier
  leakage rate.
- **"Running" means the ordered telegraph is not at stop**, surfaced or submerged.
  Past stock's total power cut (0.7 flood) the motors still take damage until the player
  orders stop. That is "put the motors out of service".
- **Defaults (provisional, Stoss to recommend):** threshold 30 % (range 5–95), amount
  10 % per game-second (range 0–100, 0 = off). Leakage halved to 2 / 7.5 %/min. The seep
  ceiling stays 0.8.

## Considered Options

- **Keep random wear and skip Stoss's idea.** Rejected: depth, water and motors would stay
  unrelated effects with no lever the player can see.
- **Run both mechanisms.** Rejected: two motor-damage sources with separate tuning.
- **Keep seepage in compartment 8 and watch compartment 4.** Rejected: on VII and IX,
  seepage would then never reach the motors.
- **Watch the seep compartment (8 on VII/IX).** Rejected: the motors aren't there.
- **Gate the damage to depth tiers.** Rejected: a boat that ascends with a flooded motor
  room would suddenly run free.
- **Key "running" to the motors' actual output.** Rejected for now: not located, and a new
  RE hunt. The ordered telegraph is already readable at `[0x0063E1D4]`.

## Consequences

- On VII and IX, unpumped seepage now cuts electric power: stock's
  `clamp(2·(flood − 0.2), 0, 1)` starts at 0.2 and is total at 0.7, below the 0.8 ceiling.
  This was accepted for the II and XXI (ticket 19) and now applies to every class.
- The crew in the motor room both pumps it and repairs the motors. The stern torpedo room
  needs no men because of depth. The historian's "stern tubes" reason for compartment 8
  is dropped; the shaft glands are at the motor room.
- APE now adds motor damage to stock breach flooding. That redraws the map's
  Out-of-scope line, which said flooding from depth charges stays at stock magnitude.
- GUI: per-tier wear amount and wear rate go. One flooding threshold (%) and one motor
  damage amount (% per game-second) come in.
- At 10 %/s a motor reaches 0.9 about 9 s after the room crosses the threshold with the
  telegraph ahead. OAE repairs it in about 4.6 game-hours.
