# 30: Seepage into the electric engine room on every class; halved leakage defaults

**What to build:** seepage goes into compartment 4, the electric engine room, on every
U-boat, not "compartment 8, else 4" (ticket 19's rule, superseded by ADR-0012). The
reader still picks the zone at run time from the zone→compartment table `0x00566898`,
never by class: it is the first zone in list order that maps to compartment 4. No such
zone means no seepage sample, with a logged reason, as before. The same flood value is
what motor flooding damage reads (ticket 31).

The leakage defaults halve, from 4 / 15 to **2 / 7.5** % per game-minute (feedback from
Stoss, 2026-11-25). The range (0–60) and the 0.8 ceiling are unchanged. The leakage
tooltip and the Review wording say "electric engine room", not "stern compartment".

**Blocked by:** none

**Status:** resolved (code; in-play check with ticket 27)

- [x] The reader picks compartment 4's zone on every class; step and reader tests that expected compartment 8 on a VII/IX are updated
- [x] Patcher defaults are 2 / 7.5; `PatchSession`/`Review` tests updated in both cultures
- [x] Leakage tooltip and Review say the electric engine room must be crewed to be pumped
- [x] `docs/re-notes/09-depth-stress-runtime-anchors.md` records the new rule, citing ADR-0012

## Comments

2026-11-25, implemented. The pick moved out of the memory walker into the pure
`depth_stress::pick_seep_zone` (step.hpp), tested at seam 1: VII/IX picks 4 not 8, II/XXI
picks 4, first in list order wins, no 4 means none. The reader collects each zone's
compartment id and calls it; no match logs "no zone maps to compartment 4 (electric engine
room)". `kDefaultConstants` and `PatchState` carry 2 / 7.5. HEAD had no reader test on
compartment 8, so the new pick tests stand in for it. Not yet checked in play: that OAE's
VII/IX zone list has a compartment-4 zone (ticket 27).
