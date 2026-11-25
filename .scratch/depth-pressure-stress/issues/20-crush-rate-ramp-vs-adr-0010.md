# Crush damage measured as a hull-scaled ramp on OAE: does ADR-0010's crush time and exemption still hold?

Type: grilling
Status: resolved
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

## Answer (grilling with the owner, 2026-11-25)

Recorded in [ADR-0011](../../../docs/adr/0011-crush-damage-is-a-hull-scaled-ramp.md),
which amends ADR-0010, and in `CONTEXT.md`.

1. **The measured ramp governs:** `hull −= CrashSpeed · dt · (D − 0.7E)/(0.3E)`, with
   `E = 1 + (CD − 1)·h`. The evidence is crush code byte-identical to vanilla and three live
   OAE points that fit within 5 %. The owner's flat rate is how the ramp looks when
   `CrashSpeed` is tiny (the test mod's VIIC took ~6,250 s): hull barely moves in a
   session, so at one depth the rate looks constant. The ramp shows only when hull falls
   far or when two depths are compared. No re-measure first: at the 5 s default the
   player sees almost no difference.
2. **Under the ramp:**
   - **Crush time is nominal.** `CrashSpeed = Hit_Points ÷ T` stays; ticket 16's patch
     stands as built. Survival from full hull scales with `HP ÷ CrashSpeed` at any depth,
     but its shape runs from about 1.3·T just past the onset to about 0.3·T at the rated
     crush depth (simulated: VIIC at T = 5 s, 6.6 s at 247 m, 1.6 s at 346 m). Per-class
     calibration to an exact T was rejected as precision no player can see. Ticket 16's GUI
     tooltip and in-play criterion need rewording; see its pointer.
   - **The exemption recomputes stock's ramp:** `CrashSpeed · dt · (D − 0.7E)/(0.3E)` from
     `+0xB4`, `+0xB8`, `+0xC0`, `+0xC4`, with D the **gauge depth**, no reference-point
     offset. The gauge reads 2.6–5 m deeper than stock's reference point, so the error is
     always toward exempting more: crush damage is never multiplied, and a hit taken while
     crushing is at worst slightly under-multiplied. The cause stamp `[obj+0x170]` was
     rejected (last cause only, ambiguous when a hit and crush share a poll).
   - **Tier 3 starts at the live onset `0.7·E`**, recomputed every poll. That is now what
     **crush depth** means; the `.zon` value is **rated crush depth**. Damage lifts the
     onset (a IIA's is 176 m at full hull, ~91 m at half). Tier 3 applies wherever it is,
     even above tier 1 or the tier-2 depth; below it, the existing rule gives tier-2 values.
3. **No re-measure.** The per-class gauge-to-reference offset isn't needed either.
