# 28: Quiet the "We have flooding, sir!" spam

Type: grilling
Status: resolved
Blocked by: none

## Parent

Found in ticket 27's first in-play session (2026-11-25). Raised by the owner.

## Problem

While seepage is active, the chief engineer repeats "CE: We have flooding, sir!" The
report is stock's, fired by the crew-report routine that reads the compartment's flood
level (`0x0043601D…`, ticket 10: reports at >0, 0.35 and 0.60). APE's seepage writes that
same float, so the report fires for water APE put there, and keeps firing.

## What the owner asked for

If and only if the flooding is due to APE's effects:

- Tier 1: replace the message with "Water seeping through the stuffing box, sir!"
- Tier 2: replace it with "More seepage, sir".
- Say it far less often: once, or at most about once an hour of game time.

Real flooding (hits, collisions) must keep stock's report.

## Open questions (none answered yet)

- **Does it fit ADR-0001?** APE never hooks a stock function. Replacing a message means
  changing what the report routine does, or the text it looks up. The cheapest route may be
  data, not code: where does the report's text and sound come from (a string table, a
  `.cfg`, a sound event)? A data change would apply to all flooding, not only APE's.
- **Telling APE's water from real water.** APE would have to know a compartment's water
  is its own: it can't see why the level rose. A compartment that is dry of damage (no
  breach) but wet is a candidate signal. Not examined.
- **Quieting without replacing.** The simpler variant: stop the repeat for APE's water
  (for example by keeping the level under the report's threshold), and say nothing. At 0.8
  that can't work; the report marks are 0>, 0.35, 0.60.
- **Is a new message worth a stock-code change at all**, against just accepting the stock
  message once per threshold crossing?

## Next

Static look at the report routine: what decides when it speaks, whether it repeats on a
timer or on a threshold crossing, and where its text comes from. Then ask the owner
whether a data-level change is acceptable.

## Static look (2026-11-25, OAE 1.5.2 `sh3.exe`)

Disassembled the report routine around `0x00436BCB`–`0x00436F1A` (part of `0x435B30`).
Per compartment, with `L` the flood level at `[[node+0x14]+0x1C]`:

- **It is a latched threshold crossing, not a timer.** One latch byte per compartment per
  band: `0 < L < 0.35` speaks string **592** ("We have flooding, sir!", latch
  `+0x32B4`); `0.35 ≤ L < 0.60` speaks **593** (heavy, `+0x32C0`); `L ≥ 0.60` speaks
  **594** (critical, `+0x32CC`). The text is looked up by id (`0x4CA800`, ids `0x250`–
  `0x252`) from `en_menu.txt` lines 592–594.
- **The latch re-arms when `L` is exactly 0** (`0x00436EED`: all three cleared), and the
  lower band's latch re-arms when the level climbs into the next band.
- **Likely cause of the repeat (inferred, not watched live):** the crew pumps only while
  `0 < L < 0.99` and clamps to 0. Seepage under the pump rate drives `L` to exactly 0,
  the latch re-arms, the next seep tick makes `L > 0`, and the report fires again. Each
  pump-dry/seep-wet cycle is one "We have flooding, sir!".

## Answers to the open questions

- **ADR-0001:** replacing the text for APE's water only means changing the routine (a
  hook) — ruled out by the ADR. A data change (`en_menu.txt` 592) is possible but changes
  every flooding report, real ones included. So the requested tier 1/tier 2 wording and
  "real flooding keeps stock's report" can't both be met without a hook.
- **Quiet without replacing — possible without a hook.** APE writes the level, so it can
  keep `L` from ever being exactly 0 while it is seeping (floor it at a tiny epsilon, such
  as 1e-6, instead of letting the pump zero it). The latch then never re-arms: one
  "We have flooding, sir!" per dive instead of one per cycle. Cost: the compartment never
  reads fully dry while seeping (F7 shows a hair of water); on surfacing APE stops
  flooring and the crew pumps it out, which re-arms the report normally. Real flooding is
  untouched.
- Telling APE's water from real water isn't needed for the floor variant: it only holds
  while APE's seepage is active.

## Decision for the owner

1. Accept **floor-at-epsilon** (one stock report per dive, no custom wording)? Recommended.
2. Or edit `en_menu.txt` 592 globally ("Water seeping through the stuffing box, sir!"),
   accepting it appears for real flooding too? No tier-2 wording either way.
3. Or reconsider a hook as a deliberate ADR-0001 exception? Not recommended.

Not implemented: no code change made, awaiting the choice. The floor variant needs one
in-play check (does the pump really clamp to 0, and does the report stop repeating).

## Implemented (2026-11-25): floor at epsilon

Owner chose option 1. While seeping, the step now raises the level to `kSeepFloor` = 0.01
(`src/mods/depth_stress/constants.hpp`, fixed, not in the struct) when a poll finds it
lower; the floor is above one pump step (0.0024 to ~0.005 per game-second), so the crew
shouldn't reach exactly 0 between polls. Unit test added; suite passes.

**Still to check in play (needs the rebuilt `ape.act`):** dive below 100 m, let the crew pump
level with seepage, and confirm "We have flooding, sir!" is said once and not repeated. If it
still repeats, the poll interval is too slow for the pump: raise the floor or poll faster.

## First in-play result and fix (2026-11-25)

The flat 0.01 floor did not stop the repeat. `ape_act.log` showed why: under time
compression each poll spans 2–7 game-seconds, and the crew pumps about 0.0033 per
game-second, so the level went 0.0067 → 0.0033 → 0.0000 between polls and re-armed the
report. The floor is now `0.01 + 0.006 × dt`, capped at 0.3 (below the 0.35 band), using
this poll's dt as the estimate for the next. Rebuilt; **still to confirm in play.**

**Confirmed in play (owner, 2026-11-25): the dt-scaled floor stops the repeat.** Custom tier 1/2 wording was not built (needs a hook, ADR-0001; or a global en_menu.txt edit).
