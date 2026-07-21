#ifndef APE_ACT_DAMAGE_LOOP_HPP
#define APE_ACT_DAMAGE_LOOP_HPP

// APE's crush-depth damage loop (post-pivot: runs alongside, never hooks).
//
// This is the orchestration layer around the pure ape::select_band seam. It is
// deliberately NOT on the automated-test seam (PRD Testing Decisions): it is
// verified manually via the debug log + Cheat-Engine-forced depths. All live
// memory access goes through IGameState, so this logic is complete now and
// issue 01's real resolver drops in behind that interface unchanged.

#include <atomic>

#include "ape_act/game_state.hpp"
#include "ape_act/log.hpp"
#include "core/damage_band.hpp"

namespace ape {

// Plausibility gates and pacing. Defaults are conservative scaffolding values;
// the depth/CDT/HP ranges will be tightened once issue 01 pins the live value
// formats and typical magnitudes. A reading outside its range → fail inert.
struct LoopLimits {
  unsigned poll_interval_ms = 100;  // loop cadence; poll frequency is free (PRD)

  double hp_min = 0.0;
  double hp_max = 1.0e6;

  double depth_min = 0.0;      // metres
  double depth_max = 3000.0;   // metres (generous; deepest plausible)

  double crush_min = 10.0;     // metres
  double crush_max = 3000.0;   // metres

  // Largest game-time step (seconds) we treat as a continuous accrual. A jump
  // beyond this (save-load, mission change, clock reset) is a discontinuity:
  // the tick is skipped and the baseline re-established, so a single huge dt
  // can never one-shot the hull.
  double dt_max_step = 3600.0;
};

// Runs until `stop` becomes true. Resolves the game-state handles (retrying
// each iteration until they come up), then each iteration samples the live
// values, gates them, computes dt = Δ(mission clock), and — when the boat is at
// or below crush depth — applies hp -= select_band(...).rate * dt by writing
// hull HP back. Every damage-applying iteration logs depth, percent-under,
// band, rate, dt, and HP applied. Any unresolved handle or failed gate fails
// inert (logged, no damage).
void run_damage_loop(std::atomic<bool>& stop, IGameState& game,
                     const BandConfig& cfg, const LoopLimits& limits,
                     Logger& log);

}  // namespace ape

#endif  // APE_ACT_DAMAGE_LOOP_HPP
