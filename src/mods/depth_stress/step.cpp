#include "mods/depth_stress/step.hpp"

namespace ape {
namespace depth_stress {

const char* implausible(const Sample& s) {
  // Written so a NaN fails every gate it reaches.
  if (!(s.max_hull > 0.0f)) return "max hull not > 0";
  if (!(s.hull >= 0.0f && s.hull <= s.max_hull)) return "hull outside [0, max]";
  if (!(s.depth_m >= 0.0f && s.depth_m < 1000.0f)) return "depth outside [0, 1000)";
  if (!(s.crush_field > 1.0f)) return "rated crush field not > 1";
  if (!(s.crush_speed >= 0.0f)) return "crush speed below 0";
  if (s.clock_s < 0) return "clock below 0";
  if (!(s.engine_port >= 0.0f && s.engine_port <= 1.0f)) return "port engine damage outside [0, 1]";
  if (!(s.engine_stbd >= 0.0f && s.engine_stbd <= 1.0f)) return "starboard engine damage outside [0, 1]";
  if (s.has_seep && !(s.seep_flood >= 0.0f && s.seep_flood <= 1.0f))
    return "seep compartment flood outside [0, 1]";
  return nullptr;
}

namespace {

void baseline(StepState& st, const Sample& s, float hull) {
  st.has_baseline = true;
  st.boat = s.boat;
  st.hull = hull;
  st.clock_s = s.clock_s;
  st.clock_was_zero = s.clock_s == 0;
}

}  // namespace

StepResult step(const Constants& c, StepState& st, const Sample* sample,
                Random* rng) {
  StepResult r;
  // 1. No sample (or one that fails a gate): nothing is written, and the
  //    next sample re-baselines.
  if (!sample) {
    st = StepState{};
    r.skipped = "no sample";
    return r;
  }
  const Sample& s = *sample;
  if (const char* why = implausible(s)) {
    st = StepState{};
    r.skipped = why;
    return r;
  }
  // 2. Baseline: a first sample, or a different boat (save-load guard 1).
  if (!st.has_baseline || st.boat != s.boat) {
    baseline(st, s, s.hull);
    r.skipped = "baseline";
    r.baselined = true;
    return r;
  }

  // 3. dt in whole game-seconds; seepage and wear (25, 26) need dt > 0.
  const std::int32_t dt = s.clock_s - st.clock_s;

  // 4. The tier, from this sample. E and the onset are stock's crush ramp
  //    (ADR-0011): E in tens of metres, the onset 0.7*E tens = 7*E metres.
  const float h = s.hull / s.max_hull;
  const float e = 1.0f + (s.crush_field - 1.0f) * h;
  const float onset_m = 7.0f * e;
  Tier tier = Tier::None;
  if (s.depth_m >= onset_m)
    tier = Tier::Three;
  else if (s.depth_m >= c.tier2_depth_m)
    tier = Tier::Two;
  else if (s.depth_m >= kTier1DepthM)
    tier = Tier::One;

  r.diag.tier = tier;
  r.diag.onset_m = onset_m;
  r.diag.dt_s = dt;
  r.diag.drop = st.hull - s.hull;

  // 5. The hull multiplier. A rise just re-baselines (the new baseline is
  //    always this poll's hull, after APE's write); a drop right after a
  //    clock-0 poll is a save restore, not damage (guard 2).
  float new_hull = s.hull;
  const float n = r.diag.drop;
  const TierConstants& k = c.tier[tier == Tier::One ? 0 : 1];
  if (tier != Tier::None && n > 0.0f && !st.clock_was_zero) {
    if (k.hull_mult > 1.0f) {
      float x = 0.0f;
      if (tier == Tier::Three) {
        // Stock's own crush share of the drop: never multiplied. D is the
        // gauge depth with no reference offset, which only exempts more.
        x = s.crush_speed * static_cast<float>(dt) *
            (s.depth_m / 10.0f - 0.7f * e) / (0.3f * e);
        if (!(x > 0.0f)) x = 0.0f;
      }
      r.diag.exempt = x;
      const float hit = n > x ? n - x : 0.0f;
      if (hit > 0.0f) {
        new_hull = s.hull - (k.hull_mult - 1.0f) * hit;
        if (new_hull < 0.0f) new_hull = 0.0f;
        r.writes.push_back({Write::Target::Hull, s.hull, new_hull});
      }
    }
  }

  // 6. Seepage: flood + rate*dt, clamped to the ceiling. At or above it there
  //    is no write, so a bigger leak from elsewhere is never lowered.
  if (tier != Tier::None && s.has_seep && k.seep_per_s > 0.0f && dt > 0 &&
      s.seep_flood < kSeepCeiling) {
    float next = s.seep_flood + k.seep_per_s * static_cast<float>(dt);
    if (next > kSeepCeiling) next = kSeepCeiling;
    if (next > s.seep_flood)
      r.writes.push_back({Write::Target::Flood, s.seep_flood, next});
  }

  // 7. Electric-engine wear: dt Bernoulli trials at q, one per game-second.
  //    Hits are tallied on local copies so a poll writes each engine once.
  if (tier != Tier::None && rng && k.wear_amount > 0.0f && dt > 0) {
    float damage[2] = {s.engine_port, s.engine_stbd};
    bool hit[2] = {false, false};
    for (std::int32_t i = 0; i < dt; ++i) {
      if (!(rng->uniform() < static_cast<double>(k.wear_chance_per_s))) continue;
      const int e = rng->uniform() < 0.5 ? 0 : 1;  // 0 port, 1 starboard
      if (damage[e] >= kWearCeiling) continue;
      const float next = damage[e] + k.wear_amount;
      damage[e] = next < kWearCeiling ? next : kWearCeiling;
      hit[e] = true;
    }
    const float old[2] = {s.engine_port, s.engine_stbd};
    const Write::Target target[2] = {Write::Target::PortEngine,
                                     Write::Target::StarboardEngine};
    for (int e = 0; e < 2; ++e)
      if (hit[e]) r.writes.push_back({target[e], old[e], damage[e]});
  }

  baseline(st, s, new_hull);
  return r;
}

}  // namespace depth_stress
}  // namespace ape
