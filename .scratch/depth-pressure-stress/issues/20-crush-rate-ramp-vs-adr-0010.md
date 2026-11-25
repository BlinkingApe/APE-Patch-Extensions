# Crush damage measured as a hull-scaled ramp on OAE: does ADR-0010's crush time and exemption still hold?

Type: grilling
Status: open
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md. Raised by
[Live values: crush depth and crush speed](17-live-crush-depth-and-speed.md)'s answer.

## Question

ADR-0010 and [the crush-time patch](16-crush-time-patch.md) rest on `CrashSpeed` being a
flat HP per second. That rests on the owner's in-game testing, and the owner restated it
on 2026-11-25: many hours of play, only ever a flat rate, whatever the depth. The owner
asked that the flat rate be assumed going forward.

The same day, the OAE lab measured otherwise (`docs/re-notes/08-oae-port-of-depth-leads.md`
§5, §7.3). At a fixed 200.0 m, a IIA lost 0.40, 0.83 and 1.77 HP/game-s at 0.92, 0.78
and 0.55 of max hull, matching the crush code's
`CrashSpeed · dt · (D − 0.7E)/(0.3E)`, with `E` the crush depth scaled by hull.

Decide, with the owner:

1. **Which reading governs the design?** Keep the flat model, or adopt the measured
   ramp? Or first find why the two disagree: the depth band played, the hull state,
   how loss was read in play, or something the lab differs in (the d3d9 wrapper moved
   aside, a IIA instead of a VIIC).
2. **If the ramp governs:**
   - what "crush time" means to a player in ticket 16's GUI field;
   - how ADR-0010's "crush damage is never multiplied" exemption is computed (the
     exact share is `CrashSpeed · dt · (D − 0.7E)/(0.3E)`, at a reference point a few
     metres off the gauge; or the cause stamp `[obj+0x170] = 4`);
   - where tier 3 starts: `0.7·E`, which moves with hull.
3. **Whether to re-measure first** on a VIIC, at two depths at constant hull: that
   separates "depends on depth" from "depends on hull".
