#include "mods/depth_stress/warning.hpp"

namespace ape {
namespace depth_stress {

float warning_level(float flood_threshold) {
  const float level = flood_threshold - kWarningBelowThreshold;
  return level > kWarningFloor ? level : kWarningFloor;
}

int warning_band(float flood) {
  if (flood >= 0.60f) return 3;
  if (flood >= 0.35f) return 2;
  return 1;
}

WarningDecision decide_warning(const Constants& c, WarningState& st,
                               const WarningInput& in) {
  WarningDecision d;

  // A swap in flight comes first, sample or not: APE's pointer must never be
  // left in the table. The first poll that sees the latch set restores it; so
  // does the timeout, and then the crossing is spent. Unsigned subtraction
  // keeps the elapsed time right across GetTickCount's wrap.
  if (st.swapped) {
    if (in.latch_set) {
      d.action = WarningAction::Restore;
    } else if (in.now_ms - st.swapped_at_ms >= kWarningTimeoutMs) {
      d.action = WarningAction::GiveUp;
    }
    if (d.action != WarningAction::None) {
      d.band = st.band;
      st.swapped = false;
    }
    return d;
  }

  const Sample* s = in.sample;
  if (!s) return d;
  const float level = warning_level(c.flood_threshold);

  // Re-arming. A different boat (a mission start or a load) starts afresh.
  // Ordering stop re-arms, so does the room falling more than the gap below the
  // level (or going dry, which the level's floor can leave out of reach).
  if (!st.has_boat || st.boat != s->boat) {
    st.has_boat = true;
    st.boat = s->boat;
    st.armed = true;
  }
  if (s->has_telegraph && s->telegraph == 0.0f) st.armed = true;
  if (s->has_seep &&
      (s->seep_flood <= 0.0f || s->seep_flood < level - kWarningRearmGap))
    st.armed = true;

  if (!s->has_seep || !s->has_telegraph) return d;
  // Off with motor flooding damage: nothing would hurt the motors.
  if (!(c.motor_damage_per_s > 0.0f)) return d;
  const bool running = s->telegraph != 0.0f;
  if (st.armed && running && s->seep_flood >= level) {
    d.action = WarningAction::Swap;
    d.band = warning_band(s->seep_flood);
    st.armed = false;
    st.swapped = true;
    st.band = d.band;
    st.swapped_at_ms = in.now_ms;
  }
  return d;
}

}  // namespace depth_stress
}  // namespace ape
