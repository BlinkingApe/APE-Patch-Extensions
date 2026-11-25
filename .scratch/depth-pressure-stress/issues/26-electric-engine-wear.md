# 26: Electric-engine wear

**What to build:** while the player boat is in a depth tier, one electric engine now and
then takes wear at the tier's amount and rate. Wear never pushes an engine past 0.9, so
the crew can always repair it. The reader follows the engine root and block-offset
chains (both AOBs already recorded) to the port and starboard damage floats. The step
runs dt Bernoulli trials at `q`, one per game-second, drawn as doubles from a 32-bit
generator seeded once (`std::mt19937` or equivalent; never MSVC `rand()`, which is
ticket 12's trap). Each hit picks port or starboard at random. The step writes
`min(d + amount, 0.9)`, makes no write at or above 0.9, and never lowers the damage.

**Blocked by:** 24 (Hull multiplier, first end-to-end slice)

**Status:** superseded by ticket 31 (ADR-0012); was done in code 2026-11-25

- [x] The step takes the generator as an input, so tests can force hits and seed runs
- [x] Step tests: under thousands of forced hits the damage never exceeds 0.9 (allowing for the 0.89999998 float32 store); no write at or above 0.9; never lowers
- [x] Step tests: over a long seeded run, hit frequency matches `q` at default rates and at high dt (time compression); both engines get picked
- [x] Step tests: no wear above 100 m, when paused, or when the clock goes back; wear amount 0 turns it off on its own; wear continues at tier-2 values below crush depth
- [ ] Injected into OAE with wear forced near-certain, engine damage appears from depth alone and the crew repairs it from 0.9 (in-play test 3, informally)

## Notes (2026-11-25)

Built: `Sample` gained `engine_port`/`engine_stbd`; `step()` takes an optional `Random*`
(null means no wear); `Mt19937Random` is the shipped generator, seeded once from
`std::random_device` in the loop. Hits in one poll are tallied, so each engine gets at
most one write per poll. The last criterion (injected into OAE) is still to do, under
ticket 27.

## Superseded (2026-11-25, ADR-0012)

Random wear is replaced by **motor flooding damage**: both motors take damage
continuously while the electric engine room is at or above a flooding threshold and
the telegraph isn't at stop, at any depth. Ticket 31 removes this ticket's generator
and wear constants and builds the replacement. The in-play criterion above is not run
for this mechanism.
