# Crush damage is a hull-scaled ramp, and crush time is nominal, amending ADR-0010

ADR-0010 rests on `CrashSpeed` being hull HP per second, a flat rate below crush depth,
from the owner's in-game testing. On 2026-11-25 the OAE lab measured otherwise, and the
crush code (byte-identical to vanilla) says why
(`docs/re-notes/08-oae-port-of-depth-leads.md` §5, §7.3). Grilling the same day
(`.scratch/depth-pressure-stress/issues/20-crush-rate-ramp-vs-adr-0010.md`) adopted the
code's reading. This ADR records it so nobody "fixes" the design back to a flat rate.

## Decided

- **Stock crush damage is a ramp.** With `h` = hull ÷ max hull and `CD` = `.zon`
  CrashDepth ÷ 10 (`+0xC0`): `E = 1 + (CD − 1)·h`, onset `0.7·E`, and below the onset
  `hull −= CrashSpeed · dt · (D − 0.7E)/(0.3E)`. As hull falls, `E` and the onset get
  shallower, so a boat held at one depth dies faster and faster. The owner's flat rate is
  the same ramp when `CrashSpeed` is tiny: hull barely moves, so at one depth the rate
  looks constant.
- **Crush depth is the live onset `0.7·E`**, and tier 3 starts there, recomputed every
  poll. The `.zon` value is the **rated crush depth**. When damage lifts the onset above
  tier 1 or the tier-2 depth, tier 3 applies wherever the onset is.
- **Crush time is nominal.** The patch still writes `CrashSpeed = Hit_Points ÷ T`.
  From full hull a boat lasts about 1.3·T just past the onset and about 0.3·T at its rated
  crush depth.
- **The exemption is the ramp share**, `CrashSpeed · dt · (D − 0.7E)/(0.3E)`, with D the
  gauge depth and no offset. The gauge reads 2.6–5 m deeper than stock's reference point,
  so the error always exempts more, never multiplies crush damage.

## Considered Options

- **Keep the flat model** (the owner's earlier instruction). Rejected once the code and
  three live points agreed and the two readings were shown to be compatible.
- **Calibrate `CrashSpeed` per class** so a boat dies in exactly T at a chosen depth.
  Rejected: at the 5 s default the spread is 2–7 s, which no player can see.
- **Use the cause stamp `[obj+0x170] = 4`** for the exemption. Rejected: it holds only the
  last cause, so a hit and crush damage in one poll are ambiguous.
- **Measure the per-class gauge-to-reference offset** for an exact exemption. Not needed:
  ignoring it only errs toward exemption.

## Consequences

- Ticket 16's patch is unchanged; its tooltip and in-play check are reworded.
- A shallow class (IIB, XXIII, rated 214 m) starts crushing at ~150 m at full hull, level
  with the 150 m tier-2 default.
