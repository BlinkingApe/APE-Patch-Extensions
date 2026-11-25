# 28: Quiet the "We have flooding, sir!" spam

Type: grilling
Status: needs-triage
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
