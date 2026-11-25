# 24: Hull multiplier, first end-to-end slice

**What to build:** in a running OAE, any hull damage the player boat takes in a depth
tier is amplified by that tier's multiplier. Crush damage itself is exempt, and a
save-load is never mistaken for damage. This slice covers every layer of `ape.act`:

- **Reader.** Gauge depth, mission clock, boat identity, hull and max hull, rated crush
  field CD and crush speed CS, through the PRD's recorded chains, producing one sample
  per poll or a reason it couldn't. The three roots with no recorded signature (boat,
  depth, clock) get one, derived from a stock instruction where possible (ADR-0003),
  and written up in `docs/re-notes/` with the OAE build hash they were proven on.
  Plausibility gates as the PRD lists them.
- **Step, the pure seam.** No sample clears state. Baseline and re-baseline (identity
  change, hull rise). `dt` in whole game-seconds. Tier from `h`, `E` and the onset at
  `7·E` m. The multiplier with the ramp-share exemption `X`, floored at 0. Save-load
  guard 2 (no amplify after a clock-0 poll). The new baseline is the hull after APE's
  write.
- **Writer.** Writes the hull to the address resolved in the same poll.
- **Logging.** Every poll that writes (D, tier, onset, dt, N, X, old → new) and every
  skipped poll's reason, rate-limited.

**Blocked by:** 21 (`ape.act` constants struct, verified loader and test harness)

**Status:** ready-for-human

- [x] Each new AOB is unique on OAE and documented with its chain and build hash; an anchor that doesn't resolve leaves the poll skipped with a logged reason
- [x] Step tests cover tier boundaries (around 100 m, the tier-2 depth and the onset) and tier 3 being lifted above both by a damaged hull
- [x] Step tests cover the multiplier at each tier, at neutral 1.0, and with N ≤ 0
- [x] Step tests cover the exemption: pure crush gives no extra damage; crush plus a hit multiplies only the hit; X is floored at 0
- [x] Step tests cover guard 1, guard 2 and a hull rise re-baselining
- [x] Step tests cover no sample: no writes, and the next sample re-baselines
- [x] Step tests cover a failed plausibility gate, which writes nothing
- [ ] Injected into OAE by the lab method, a hit at tier-2 depth removes `mult ×` the stock delta, as seen in the log (in-play tests 1 and 5, informally; ticket 27 runs them formally)

## Comments

**2026-11-25, code done; the in-play check is left.** Everything but the last box is
implemented and tested. Status is ready-for-human because the last box needs a live OAE
session.

- **Step** (`src/mods/depth_stress/step.{hpp,cpp}`): PRD rules 1–5 for the hull
  multiplier.
  - The plausibility gates are a pure `implausible()`. A sample that fails one is treated
    as no sample.
  - `StepResult` carries `Diagnostics` for the log (tier, onset, dt, N, X), and each write
    carries its old value.
  - Tests: 14 new doctest cases in `tests/depth_stress_test.cpp`. Expected values are
    worked by hand (for example crush plus a hit: 473.40 with X 6.8053).
- **Reader and writer** (`src/ape_act/game_memory.{hpp,cpp}`, scanner
  `src/core/aob.hpp`): three new signatures, written up in
  `docs/re-notes/09-depth-stress-runtime-anchors.md`.
  - The boat root and the depth root are anchored on stock instructions. The clock has no
    stock reference, so it is anchored on h.sie's cave.
  - Each signature is unique across all executable sections on `20360fe8`, and also on OAE
    2.0, KC 2.5 and the reference exe.
  - Reads and writes go through `Read/WriteProcessMemory` on the process itself, so a bad
    pointer fails instead of faulting.
- **Loop** (`src/ape_act/dllmain.cpp`):
  - Anchors are resolved once, on the thread.
  - Every write is logged, and so is each new boat's baseline.
  - Skips are rate-limited: when the reason changes, at most once per second; the same
    reason, every 30 s with a count.
  - A failed hull write clears the step state, so the next drop isn't measured from a hull
    the game never held.
- **Left: the in-play check (ticket 27 runs it formally).** Inject the built ape.act by
  the lab method, take a hit at tier-2 depth, and confirm `ape_act.log` shows
  `old − new = (mult − 1)·(N − X)`.
