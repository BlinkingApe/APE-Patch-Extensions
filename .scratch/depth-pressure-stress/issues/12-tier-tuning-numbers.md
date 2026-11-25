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

## Lead from sh3-crush-pressure (2026-11-25): proposed defaults for seepage, wear and the hard ceiling

**vanilla Steam 1.4b; portability: owner.** Reasoned from facts measured on the
unwrapped vanilla Steam SH3 1.4b exe, sha256
`1f14568235f903e7e50b73d3caa937b6946aadf7013e3dc39dfd1cc7975986f2`, VIIC, realistic
repair time on (tickets 10 and 11's leads above). **Proposals only:** this ticket
decides the real values, and it is **not** resolved by this section. The tier
depths and the hull multiplier are **not** proposed: nothing measured there bears
on them.

| Field | Unit | Tier 1 | Tier 2 | Valid range |
|---|---|---|---|---|
| `seep_rate_t1`, `seep_rate_t2` | flood fraction of the astern compartment per game-second | **0.0008** | **0.0036** | 0 – 0.01 |
| `seep_ceiling` (**new field**) | flood fraction | **0.8** (both tiers) | | 0 – 0.98 |
| `wear_chance_t1`, `wear_chance_t2` | probability of a wear hit per game-second | **1/7200 ≈ 1.389e-4** | **1/1200 ≈ 8.333e-4** | 0 – 0.01 |
| `wear_min_tN` – `wear_max_tN` | damage added per hit, to one engine picked at random | **0.05 – 0.20** | **0.15 – 0.45** | 0 – `hard_ceiling`, min ≤ max |
| `hard_ceiling` | equipment damage | **0.9** (both tiers) | | 0.05 – 0.98 |

All float32 in `ape.act`'s constants struct (ADR-0006); each is stored within
~4e-8 of itself (0.9 as 0.89999998, so the clamp can't round past it).

- **Seeps against the measured pump** (0.00243/game-s fresh, ~0.0017 tired).
  Tier 1 is ⅓ of the fresh pump: held by any watch crew down to strength 0.24 — a
  running cost, the room never quite dry. Tier 2 is 1.5× the pump: heavy flooding
  at ~300 game-s, critical ~510, the seep ceiling ~685 (181/310/414 tired). The way
  out is to ascend to tier 1 or send the damage-control team aft (team at 0.5 turns
  the race). With realistic repair time off the pump doubles and tier 2 is held;
  0.0060 would keep it losing there.
- **`seep_ceiling`, proposed as a new field.** Seepage never fills the room past
  0.8: up to 0.8 a VIIC kept depth with way on; at 0.99 the room's crew goes down,
  the pump stops, and the boat was later lost. Hours at 0.8 weren't measured; if
  play shows a slow loss there, drop it to 0.6.
- **Ceiling against the measured threshold (1.0, exclusive): 0.9.** Margin 0.1 to
  the threshold and 0.09 to the "destroyed" message at 0.99; F7 still orange at 0.9.
  Rule: if `d ≥ hard_ceiling`, don't write; else write `min(d + hit, hard_ceiling)`.
  APE never lowers a value, so a stock hit above the ceiling is left alone (and
  can still reach 1.0 from APE's raised base).
- **Accumulated tail (20,000 simulated patrols per row, repair racing wear).**
  Unrepaired, four tier-2 hours sum to 3.6 of wear: 99 % of such patrols reach the
  ceiling, and stop there by construction. With the watch crew repairing, the worse
  engine peaks at 0.44 (median), 0.78 (99th percentile), and touches the ceiling in
  0.24 % of 4-hour tier-2 patrols (1.5 % at 24 h). Tier 1 never passes ~0.25 with
  any crew. To keep 24 h under 1 %, halve `wear_chance_t2` or narrow the band to
  0.15–0.35.
- **The `rand()` trap (the diesel lesson, upward this time).** MSVC `rand()` has
  15 bits: tier 1's 1.389e-4 per game-second fires at 1.526e-4 (**+9.9 %**), and
  drawn per 100-ms poll its 1.389e-5 is under one `rand()` step and fires at
  3.05e-5 (**+120 %**). **Advice:** draw with a 32-bit generator into a double
  (e.g. `std::mt19937` + `uniform_real_distribution<double>`), once per whole
  game-second of Δ(mission clock), never per poll. At high time compression run
  `dt` trials (or one draw of `1 − (1 − q)^dt` in double), and take seepage as one
  larger step clamped at the seep ceiling. Float32 itself is nowhere near an
  absorption edge.
- **Assumptions:** 10 Hz poll with dt from the int mission clock; 4 tier-2 and 24
  tier-1 game-hours a patrol; realistic repair time on; VIIC; one engine per hit.

Full write-up, in the sh3-crush-pressure repo (`d:\code\sh3-crush-pressure`, branch
`automated-depth-re` until merged): `docs/re-notes/proposed-defaults.md` (§1 the
table, §3 seepage, §4 ceiling, §5 tail, §6 float32 and `rand()`, §7 assumptions);
the arithmetic is `harness/defaults.py` (`python -m harness.defaults`). Inputs:
`docs/findings/pump-rate.json`, `docs/findings/repair-threshold.json`.

## Decisions from grilling (2026-11-25): the patcher's field list, units, ranges and defaults

Settled with the owner after the sh3-crush-pressure lead above. This settles the field
list. It is **not** an `## Answer`: the seep and wear values still rest on vanilla-1.4b
measurements until tickets `10` and `11` are ported to OAE. Recorded in ADR-0010 and `CONTEXT.md`.

**Tiers.** Tier 1 runs from **100 m (fixed, no field)** to the tier-2 depth. Tier 2 runs
from the tier-2 depth to crush depth, which `ape.act` reads live (ticket `17`). Tier 3
is below crush depth: stock's crush damage at APE's crush speed (ticket `16`). Seepage,
wear and the hull multiplier carry on there at **tier-2 values**, but crush damage itself
is never multiplied: APE treats `crush speed × elapsed game-seconds` of each drop as
crush damage and multiplies only the rest.

| Field | Tier | Unit shown to players | Range | Default |
|---|---|---|---|---|
| Tier-2 depth | T2 | metres | 101–300 | **150** |
| Leakage (seepage) rate | T1, T2 | % of the stern compartment per game-minute | from ticket 12's 0–0.01/s (0–60 %/min) | 4.8 / 21.6 (lead's 0.0008 / 0.0036 per s) |
| Electric-engine wear amount | T1, T2 | % of one engine's health per hit, engine picked at random | 1–90 % | 10 / 30 |
| Electric-engine wear rate | T1, T2 | average game-minutes between hits | (to set from the lead's 0–0.01/s chance) | 120 / 20 |
| Hull multiplier | T1, T2 | × | 1.0–5.0 | 1.25 / 1.5 |
| Crush time | T3 | seconds | 1–3600 | 5 (ticket `16`) |

- **One fixed wear amount per hit replaces the lead's random ranges** (0.05–0.20 /
  0.15–0.45). Defaults are those ranges' midpoints, so the mean wear per hour is unchanged.
- **Not exposed, fixed in `ape.act`:** seep ceiling **0.8** and electric-engine hard
  ceiling **0.9**, the repairability and survivability guarantees. Wear never writes past
  0.9 whatever the amount; seepage never fills past 0.8.
- **Tier-2 depth vs shallow-crushing classes:** one depth for every boat. A class whose
  crush depth is above the tier-2 depth (the IIB and XXIII crush at 214 m) goes from
  tier 1 straight to tier 3. The field's tooltip says so.
- **Terms:** "seepage" in the glossary, "Leakage" in the GUI; "electric-engine wear";
  "hull multiplier"; "crush time".
