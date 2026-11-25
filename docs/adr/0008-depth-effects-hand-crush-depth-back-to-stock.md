# APE hands crush-depth damage back to stock; depth effects apply above it as poll-and-amplify stress

> **Amended by ADR-0010 (2026-11-25):** APE now sets each U-boat's `CrashSpeed` (from a
> player-chosen crush time) and reads crush depth live as tier 2's floor. Tier 3 is still
> stock's mechanism. The "never reads CDT / never touches `CrashSpeed`" lines below are
> superseded.

The original crush-depth-damage spec had APE own the entire model below crush depth:
three fraction-of-crush-depth bands of flat HP-per-second bleed, with stock's own
`CrashSpeed` neutralized (`=0` in shipped per-unit data) so APE was the sole author of
damage past the threshold. That model is reversed. This ADR records the reversal and why,
since nothing else does.

## Context

The historian's premise (`.scratch/depth-pressure-stress/map.md` Notes, 2026-08-16): most
u-boat classes were designed to a ~100m operating depth with a 2.5× safety margin (crush
~250m), standard German practice. Shallower than 100m, there shouldn't be problems; below
it, the hull is under increasing stress the deeper you go, and secondary systems — cooling
water, thrust and motor bearings — suffer too, since their lines are only pressure-tight to
design depth.

That premise doesn't fit "APE owns 100% below crush depth, three bleed bands." It fits
depth as a *continuum of stress* starting well before the catastrophic crush-depth case
stock already models, with a mechanism (not just hull HP) attached to the systems the
historian named.

## Decided

**APE no longer owns crush-depth damage. It adds depth-proportional stress above it, and
hands the catastrophic case back to stock entirely.**

- **Tier 3 (below crush depth) is stock's own damage, untouched.** APE never reads CDT and
  never neutralizes `CrashSpeed`. This is a full reversal of the previous "APE owns 100%"
  decision.
- **Tiers 1 and 2 are configured *absolute* depths** (default ~100m / deeper), independent
  of each boat's own crush depth — the historian's premise is stated in absolute terms, not
  as a fraction of a per-class value.
- **The tier effect is a hull-damage *multiplier*, not a flat rate.** APE polls hull HP,
  notices it fell by `N` since the last tick (from whatever stock/other-mod cause), and
  subtracts a further `(mult − 1) · N`. It amplifies whatever damage is already happening;
  it does not compute or attribute its own baseline rate.
- **Two more effects ride the same tiers:** a flat astern seepage rate (continuous flooding
  pressure) and a flat electric-engine wear chance — the systems the historian named,
  narrowed to one system for the first cut (ticket `11`'s scope note).

## Why poll-and-amplify, not interception

ADR-0001 already forbids hooking, replacing, or routing through any stock SH3 function —
`ape.act` only reads and writes memory, alongside the game. A multiplier is, definitionally,
an operation on *something else's* damage: to multiply a value you'd otherwise need to see
it being computed, which means either hooking the writer or reading before/after and
inferring the delta. ADR-0001 rules out the first option, so poll-and-amplify — read hull HP
each tick, infer the delta since last tick, amplify that delta — is the only shape left
that keeps the alongside principle intact.

**Consequence, and a real limitation:** APE cannot attribute an HP drop to a cause. If hull
HP fell by 5 between polls, APE cannot tell whether that was a depth-charge, gunfire,
grounding, or crush-depth pressure — it amplifies all of them equally while the boat is in
a tier. This is accepted, not fixed: distinguishing causes would mean intercepting the
generic collision solver's damage writes (the same solver `01-crush-depth-damage-signature.md`
§3 already found synthesizes crush-depth pressure as a generic impact), which is exactly
the interception ADR-0001 rules out. A future contributor debugging "why did the multiplier
fire on a depth-charge hit at 120m" should find this ADR before assuming it's a bug.

## Consequences

- The pure band-decision function and its config (old tickets `03`/`04`) are dead — deleted
  by ticket 13, not reworked. The tier model's own pure seam (multiplier/seep/wear given a
  depth and the tier config) is new work, specced when the PRD is rewritten.
- The `CrashSpeed`=0 neutralization edit and the JSGME packaging built around it (old ticket
  `07`) are dead — deleted by ticket 13. There is no data edit left to ship.
- `CONTEXT.md`'s glossary reverses: "damage window / band" (fraction-of-crush-depth,
  APE-owns-100%) is replaced by "depth tier" (absolute depth, multiplier + seep + wear,
  tier 3 untouched).
- CDT is dropped from APE's live-value worklist entirely (tickets `08`/`09`, amended
  2026-08-17) — it was never live-value #3 under the new model.
