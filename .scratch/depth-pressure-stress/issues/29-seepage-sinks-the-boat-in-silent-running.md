# 29: Seepage sinks the boat when the crew isn't pumping (silent running)

Type: grilling
Status: needs-triage
Blocked by: none

## Parent

Found in ticket 27's first in-play session (2026-11-25). Raised by the owner.

## Observed

- Tier 1 starts leaking below 100 m, and the rate rises below 150 m. Both correct.
- At normal running the leak is trivial: the crew pumps it easily.
- **In silent running the crew doesn't pump, and even tier-1 seepage soon sinks the boat.**
  This held with Stiebler's "Silent Running Fix" disabled. The owner assumes that should
  be the default whenever APE's depth stress is on.

## Why it matters

The PRD's promise: seepage never fills a compartment past 0.8 "so that depth stress alone
never drowns the room's crew and sinks me" (user story 8). The ceiling protects the room's
crew, but ticket 10's live data already showed 0.5 of stern water took the boat from 30 to
67 m, so 0.8 is enough to sink it. The ceiling and the story disagree.

## Open questions

- **Is it intended?** A boat that can't pump while silent at depth is realistic, but a
  tier-1 leak (4 %/min, ~20 min to the ceiling) sinking a boat that just sits still at
  110 m is probably harsher than the design meant.
- **Why doesn't the crew pump in silent running?** Stock behaviour or h.sie's change?
  Not examined. What does the "Silent Running Fix" change?
- **Options:** (a) lower the fixed seep ceiling to a level the boat can carry (needs
  measuring: what flood in compartment 8 or 4 makes a VII/IX/II sink or lose its dive
  planes?); (b) stop seepage during silent running (needs an anchor for the state);
  (c) leave it and document it ("silent running at depth is dangerous"); (d) the patcher
  recommends or sets h.sie's switch when depth stress is on.
- The ceiling is fixed in code (`kSeepCeiling`, 0.8), not a field. Option (a) changes a
  PRD number.

## Next

Owner's call on intent (b/c/a). If (a), measure: what flood level sinks each class.
