# Tuning: what the tier boundaries, multiplier, seep rate and wear chance actually are

Type: grilling
Status: open
Blocked by: 10, 11

## Parent

.scratch/depth-pressure-stress/map.md

## Question

The model's *shape* is settled; its *numbers* are not. Decide the shipped defaults
and the valid ranges for every patcher field:

- **Tier 1 depth** (default ~100m per the historian's near-universal operating depth)
  and **tier 2 depth** — the second boundary has no proposed value at all yet.
- **Hull damage multiplier**, per tier. What does "more so" mean — 1.5× and 2×? Is
  tier 1's multiplier even above 1.0, or is tier 1 seep-and-wear only?
- **Astern seep rate**, per tier, in game-seconds — judged against ticket 10's
  measured crew pump rate, so that tier 1 is a running cost the crew can hold and
  tier 2 is a race they are losing.
- **Electric-engine wear chance and magnitude**, per tier, plus the **hard ceiling**
  (ticket 11 establishes the empirical repair threshold; this ticket picks the margin
  below it).

Two lessons from the diesel work apply directly and should be treated as constraints,
not suggestions:

1. **Check representability before committing to a small constant.** Umbrella ticket
   04 picked a probability ~5,000× smaller than h.sie's and put it under float32
   epsilon, where a single `fstp dword` quantized it into silent inertness. APE's
   arithmetic here is its own C++ rather than a patched cave, so the trap is far less
   likely — but any per-tick probability small enough to matter over hours should be
   sanity-checked against the type it is stored and multiplied in.
2. **Reason about the accumulated tail, not the per-event number.** Diesel's
   `0.15…0.55` band looked safe and reached `1.0` on two hits 3.1% of the time. Depth
   wear is always-on, so the relevant question is what a full patrol spent at tier 2
   accumulates to — which the hard ceiling bounds, but the ceiling should be chosen
   knowing that distribution rather than in spite of it.

Blocked because both the seep rate and the wear ceiling are meaningless numbers until
ticket 10 measures the pump rate and ticket 11 measures the repair threshold.

## Acceptance criteria

- [ ] Every patcher field named, with a default, a valid range, and a unit.
- [ ] Seep defaults justified against the measured pump rate.
- [ ] Ceiling justified against the measured repair threshold, with stated margin.
- [ ] The per-patrol accumulation of tier-2 wear reasoned about explicitly.
