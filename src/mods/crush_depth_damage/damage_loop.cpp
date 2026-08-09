#include "mods/crush_depth_damage/damage_loop.hpp"

#include <windows.h>

#include <cmath>
#include <string>

namespace ape {
namespace {

const char* band_name(Band b) {
  switch (b) {
    case Band::Light: return "light";
    case Band::Medium: return "medium";
    case Band::Heavy: return "heavy";
  }
  return "?";
}

bool in_range(double v, double lo, double hi) {
  return std::isfinite(v) && v >= lo && v <= hi;
}

// Reason a reading set is rejected, or nullptr if it passes every gate.
const char* gate_reason(const Readings& r, const LoopLimits& lim) {
  if (!in_range(r.crush_depth, lim.crush_min, lim.crush_max)) return "crush_depth out of range";
  if (!in_range(r.depth, lim.depth_min, lim.depth_max)) return "depth out of range";
  if (!in_range(r.hull_hp, lim.hp_min, lim.hp_max)) return "hull_hp out of range";
  if (!std::isfinite(r.mission_clock) || r.mission_clock < 0.0) return "mission_clock invalid";
  return nullptr;
}

}  // namespace

void run_damage_loop(std::atomic<bool>& stop, IGameState& game,
                     const BandConfig& cfg, const LoopLimits& limits,
                     Logger& log) {
  bool have_baseline = false;
  double prev_clock = 0.0;
  std::string last_inert;  // de-dupe repeated fail-inert log lines

  auto note_inert = [&](const std::string& why) {
    if (why != last_inert) {
      log.logf("[inert] %s", why.c_str());
      last_inert = why;
    }
    have_baseline = false;  // don't accrue across a gap
  };

  log.logf("[loop] started");

  while (!stop.load(std::memory_order_relaxed)) {
    std::string why;

    if (!game.resolved()) {
      if (!game.resolve(why)) {
        note_inert("unresolved: " + why);
        Sleep(limits.poll_interval_ms);
        continue;
      }
      log.logf("[loop] anchors resolved");
      last_inert.clear();
    }

    Readings r;
    if (!game.sample(r, why)) {
      note_inert("sample failed: " + why);
      Sleep(limits.poll_interval_ms);
      continue;
    }

    if (const char* bad = gate_reason(r, limits)) {
      note_inert(std::string("gate: ") + bad);
      Sleep(limits.poll_interval_ms);
      continue;
    }
    last_inert.clear();

    // dt from the mission clock. First good sample only establishes a baseline.
    if (!have_baseline) {
      prev_clock = r.mission_clock;
      have_baseline = true;
      Sleep(limits.poll_interval_ms);
      continue;
    }
    const double dt = r.mission_clock - prev_clock;
    prev_clock = r.mission_clock;

    if (dt < 0.0 || dt > limits.dt_max_step) {
      // Clock ran backwards or jumped (load / mission change) — discontinuity.
      note_inert("clock discontinuity");
      Sleep(limits.poll_interval_ms);
      continue;
    }
    if (dt == 0.0) {
      // Paused (or same tick): no game-time elapsed, so no damage. Not inert —
      // baseline stays valid.
      Sleep(limits.poll_interval_ms);
      continue;
    }

    // Only apply at or below crush depth; shallower is safe (no accrual).
    if (r.depth >= r.crush_depth) {
      const BandResult res = select_band(r.depth, r.crush_depth, cfg);
      const double under = (r.depth - r.crush_depth) / r.crush_depth;  // >= 0 here
      const double applied = res.rate * dt;
      double new_hp = r.hull_hp - applied;
      if (new_hp < 0.0) new_hp = 0.0;  // boat dies naturally at 0

      if (game.write_hull_hp(new_hp, why)) {
        log.logf("[dmg] depth=%.1f under=%.1f%% band=%s rate=%.4f dt=%.4f hp:%.2f->%.2f",
                 r.depth, under * 100.0, band_name(res.band), res.rate, dt,
                 r.hull_hp, new_hp);
      } else {
        note_inert("write failed: " + why);
      }
    }

    Sleep(limits.poll_interval_ms);
  }

  log.logf("[loop] stopped");
}

}  // namespace ape
