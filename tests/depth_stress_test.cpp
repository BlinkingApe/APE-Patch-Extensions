// Seam 1 (PRD Testing Decisions): the pure depth-stress code, under vendored
// doctest. No Windows headers, no game memory.

#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "doctest.h"

#include <cmath>
#include <cstring>
#include <limits>
#include <vector>

#include "mods/depth_stress/constants.hpp"
#include "mods/depth_stress/step.hpp"

namespace ds = ape::depth_stress;

TEST_CASE("the shipped constants carry the current defaults in runtime units") {
  const ds::Constants c = ds::kDefaultConstants;
  // Patcher conversions (PRD Fields): leakage p %/min -> p/6000 per game-s,
  // flooding threshold t % -> t/100, motor damage a %/game-s -> a/100.
  CHECK(c.tier2_depth_m == doctest::Approx(150.0));
  // Leakage halved to 2 / 7.5 %/min (ticket 30, ADR-0012).
  CHECK(c.tier[0].seep_per_s == doctest::Approx(2.0 / 6000.0));
  CHECK(c.tier[1].seep_per_s == doctest::Approx(7.5 / 6000.0));
  // Motor flooding damage (ticket 31, ADR-0012): 30 %, 10 %/game-s.
  CHECK(c.flood_threshold == doctest::Approx(0.3));
  CHECK(c.motor_damage_per_s == doctest::Approx(0.1));
  CHECK(c.tier[0].hull_mult == doctest::Approx(1.25));
  CHECK(c.tier[1].hull_mult == doctest::Approx(1.5));
}

TEST_CASE("the shipped constants verify") {
  const ds::Verdict v = ds::verify(ds::kDefaultConstants);
  CHECK(v.ok);
}

TEST_CASE("a corrupted marker fails the verdict") {
  for (int i = 0; i < 8; ++i) {
    ds::Constants c = ds::kDefaultConstants;
    c.magic[i] ^= 0x20;
    CAPTURE(i);
    CHECK_FALSE(ds::verify(c).ok);
  }
}

TEST_CASE("a different version fails the verdict") {
  ds::Constants c = ds::kDefaultConstants;
  c.version = ds::kVersion + 1;
  CHECK_FALSE(ds::verify(c).ok);
  c.version = ds::kVersion - 1;
  CHECK_FALSE(ds::verify(c).ok);
}

TEST_CASE("step: with no sample, nothing is written") {
  ds::StepState state{};
  const ds::StepResult r = ds::step(ds::kDefaultConstants, state, nullptr);
  CHECK(r.writes.empty());
}

// -- the hull multiplier (ticket 24) -----------------------------------------
//
// A Type VIIC-like boat: 500 HP, rated crush depth 230 m (CD 23), so at full
// hull stock's crush onset is 7 * 23 = 161 m.

namespace {

constexpr std::uint32_t kBoat = 0x1234560;

ds::Sample at(float depth_m, float hull, std::int32_t clock_s,
              std::uint32_t boat = kBoat) {
  ds::Sample s{};
  s.boat = boat;
  s.clock_s = clock_s;
  s.depth_m = depth_m;
  s.hull = hull;
  s.max_hull = 500.0f;
  s.crush_field = 23.0f;
  s.crush_speed = 0.0f;
  return s;
}

// Feeds a baseline, then the second sample; returns the second poll's result.
ds::StepResult after_baseline(const ds::Constants& c, ds::StepState& st,
                              const ds::Sample& first,
                              const ds::Sample& second) {
  ds::step(c, st, &first);
  return ds::step(c, st, &second);
}

float hull_written(const ds::StepResult& r) {
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].target == ds::Write::Target::Hull);
  return r.writes[0].value;
}

}  // namespace

TEST_CASE("multiplier: a hit at tier-2 depth is amplified by the tier-2 mult") {
  ds::StepState st{};
  // 150 m is the default tier-2 depth; at 480 HP the onset is 154.8 m.
  const ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                          at(152.0f, 500.0f, 100),
                                          at(152.0f, 480.0f, 101));
  // 20 HP stock hit, x1.5: APE removes another 10.
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(470.0));
  CHECK(r.writes[0].old_value == doctest::Approx(480.0));
  CHECK(r.diag.tier == ds::Tier::Two);
}

TEST_CASE("multiplier: tier 1 uses the tier-1 mult, and above 100 m nothing") {
  ds::StepState st{};
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                    at(120.0f, 500.0f, 10), at(120.0f, 480.0f, 11));
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(475.0));  // 20 x 1.25
  CHECK(r.diag.tier == ds::Tier::One);

  ds::StepState shallow{};
  r = after_baseline(ds::kDefaultConstants, shallow,
                     at(60.0f, 500.0f, 10), at(60.0f, 480.0f, 11));
  CHECK(r.writes.empty());
  CHECK(r.diag.tier == ds::Tier::None);
}

TEST_CASE("tier boundaries: 100 m, the tier-2 depth and the onset") {
  struct Case { float depth; ds::Tier tier; };
  // Hull 499 of 500: E = 1 + 22 * 0.998 = 22.956, onset 160.692 m.
  const Case cases[] = {
      {99.9f, ds::Tier::None},  {100.0f, ds::Tier::One},
      {149.9f, ds::Tier::One},  {150.0f, ds::Tier::Two},
      {160.6f, ds::Tier::Two},  {160.8f, ds::Tier::Three},
  };
  for (const Case& k : cases) {
    CAPTURE(k.depth);
    ds::StepState st{};
    const ds::StepResult r = after_baseline(
        ds::kDefaultConstants, st, at(k.depth, 500.0f, 1), at(k.depth, 499.0f, 2));
    CHECK(r.diag.tier == k.tier);
  }
}

TEST_CASE("tier 3 is lifted above the tier-2 depth and 100 m by a damaged hull") {
  // Hull 250 of 500: E = 1 + 22 * 0.5 = 12, onset 84 m.
  ds::StepState st{};
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                    at(90.0f, 260.0f, 1), at(90.0f, 250.0f, 2));
  CHECK(r.diag.tier == ds::Tier::Three);
  CHECK(r.diag.onset_m == doctest::Approx(84.0));
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(245.0));  // tier 3 takes the tier-2 mult

  ds::StepState shallow{};
  r = after_baseline(ds::kDefaultConstants, shallow,
                     at(80.0f, 260.0f, 1), at(80.0f, 250.0f, 2));
  CHECK(r.diag.tier == ds::Tier::None);
  CHECK(r.writes.empty());
}

TEST_CASE("multiplier: neutral 1.0, no drop, and a floor at 0") {
  ds::Constants c = ds::kDefaultConstants;
  c.tier[1].hull_mult = 1.0f;
  ds::StepState st{};
  ds::StepResult r = after_baseline(c, st, at(152.0f, 500.0f, 1), at(152.0f, 480.0f, 2));
  CHECK(r.writes.empty());

  ds::StepState same{};
  r = after_baseline(ds::kDefaultConstants, same,
                     at(152.0f, 480.0f, 1), at(152.0f, 480.0f, 2));
  CHECK(r.writes.empty());

  c.tier[1].hull_mult = 5.0f;
  ds::StepState low{};
  // 30 -> 10 at 300 m: tier 3 (CS 0, so nothing exempt); 10 - 4 * 20 < 0.
  r = after_baseline(c, low, at(300.0f, 30.0f, 1), at(300.0f, 10.0f, 2));
  const float hull = hull_written(r);
  CHECK(hull == 0.0f);
}

namespace {

// At 200 m with crush speed 10 and dt 1. For hull H the ramp share is
// X = 10 * (20 - 0.7E) / (0.3E) with E = 1 + 22 * H/500.
ds::Sample crushing(float hull, std::int32_t clock_s) {
  ds::Sample s = at(200.0f, hull, clock_s);
  s.crush_speed = 10.0f;
  return s;
}

}  // namespace

TEST_CASE("exemption: pure crush damage is never multiplied") {
  // 500 -> 495: E = 22.78, X = 10 * (20 - 15.946) / 6.834 = 5.93 >= N = 5.
  ds::StepState st{};
  const ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                          crushing(500.0f, 1), crushing(495.0f, 2));
  CHECK(r.diag.tier == ds::Tier::Three);
  CHECK(r.writes.empty());
}

TEST_CASE("exemption: crush plus a hit multiplies only the hit") {
  // 500 -> 480: E = 22.12, X = 10 * (20 - 15.484) / 6.636 = 6.8053.
  // 480 - 0.5 * (20 - 6.8053) = 473.40.
  ds::StepState st{};
  const ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                          crushing(500.0f, 1), crushing(480.0f, 2));
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(473.40).epsilon(1e-4));
  CHECK(r.diag.exempt == doctest::Approx(6.8053).epsilon(1e-3));
}

TEST_CASE("exemption: X is floored at 0 when the clock went back") {
  // dt = -100 would make X negative and amplify more than the drop.
  ds::StepState st{};
  const ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                          crushing(500.0f, 200), crushing(480.0f, 100));
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(470.0));
  CHECK(r.diag.exempt == 0.0f);
}

TEST_CASE("save-load guard 1: a new boat identity re-baselines") {
  ds::StepState st{};
  ds::Sample first = at(152.0f, 500.0f, 10);
  ds::Sample loaded = at(152.0f, 300.0f, 11, kBoat + 0x1000);
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st, first, loaded);
  CHECK(r.writes.empty());

  const ds::Sample hit = at(152.0f, 290.0f, 12, kBoat + 0x1000);
  r = ds::step(ds::kDefaultConstants, st, &hit);
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(285.0));  // from 300, not 500
}

TEST_CASE("save-load guard 2: no amplify after a poll whose clock read 0") {
  ds::StepState st{};
  const ds::Sample a = at(152.0f, 500.0f, 0);
  const ds::Sample restored = at(152.0f, 400.0f, 1);
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st, a, restored);
  CHECK(r.writes.empty());

  const ds::Sample hit = at(152.0f, 380.0f, 2);
  r = ds::step(ds::kDefaultConstants, st, &hit);
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(370.0));  // from 400
}

TEST_CASE("a hull rise re-baselines without amplifying") {
  ds::StepState st{};
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                    at(152.0f, 480.0f, 1), at(152.0f, 500.0f, 2));
  CHECK(r.writes.empty());
  const ds::Sample hit = at(152.0f, 490.0f, 3);
  r = ds::step(ds::kDefaultConstants, st, &hit);
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(485.0));  // from 500
}

TEST_CASE("the new baseline is the hull after APE's write") {
  ds::StepState st{};
  ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                    at(152.0f, 500.0f, 1), at(152.0f, 480.0f, 2));
  REQUIRE(r.writes.size() == 1);  // 480 -> 470
  // Stock doesn't drop again; the game now reads APE's 470: no new drop.
  const ds::Sample settled = at(152.0f, 470.0f, 3);
  r = ds::step(ds::kDefaultConstants, st, &settled);
  CHECK(r.writes.empty());
}

TEST_CASE("no sample: no writes, and the next sample re-baselines") {
  ds::StepState st{};
  const ds::Sample a = at(152.0f, 500.0f, 1);
  ds::step(ds::kDefaultConstants, st, &a);
  ds::StepResult r = ds::step(ds::kDefaultConstants, st, nullptr);
  CHECK(r.writes.empty());

  const ds::Sample after_gap = at(152.0f, 480.0f, 5);
  r = ds::step(ds::kDefaultConstants, st, &after_gap);
  CHECK(r.writes.empty());  // re-baselined at 480, not amplified from 500
  const ds::Sample hit = at(152.0f, 460.0f, 6);
  r = ds::step(ds::kDefaultConstants, st, &hit);
  const float hull = hull_written(r);
  CHECK(hull == doctest::Approx(450.0));
}

TEST_CASE("a failed plausibility gate writes nothing and re-baselines") {
  struct Bad { const char* what; ds::Sample s; };
  ds::Sample over_max = at(152.0f, 501.0f, 2);
  ds::Sample negative_hull = at(152.0f, -1.0f, 2);
  ds::Sample zero_max = at(152.0f, 0.0f, 2);
  zero_max.max_hull = 0.0f;
  ds::Sample deep = at(1000.0f, 480.0f, 2);
  ds::Sample above_water = at(-0.5f, 480.0f, 2);
  ds::Sample flat_cd = at(152.0f, 480.0f, 2);
  flat_cd.crush_field = 1.0f;
  ds::Sample negative_cs = at(152.0f, 480.0f, 2);
  negative_cs.crush_speed = -0.1f;
  ds::Sample negative_clock = at(152.0f, 480.0f, -1);
  const Bad bad[] = {
      {"hull above max", over_max},  {"hull below 0", negative_hull},
      {"max not > 0", zero_max},     {"D at 1000", deep},
      {"D below 0", above_water},    {"CD not > 1", flat_cd},
      {"CS below 0", negative_cs},   {"clock below 0", negative_clock},
  };
  for (const Bad& b : bad) {
    CAPTURE(b.what);
    CHECK(ds::implausible(b.s) != nullptr);
    ds::StepState st{};
    ds::StepResult r = after_baseline(ds::kDefaultConstants, st,
                                      at(152.0f, 500.0f, 1), b.s);
    CHECK(r.writes.empty());
    CHECK(r.skipped != nullptr);
    // Treated as no sample: the next good one re-baselines.
    const ds::Sample next = at(152.0f, 470.0f, 3);
    r = ds::step(ds::kDefaultConstants, st, &next);
    CHECK(r.writes.empty());
  }
  CHECK(ds::implausible(at(152.0f, 480.0f, 0)) == nullptr);
}

TEST_CASE("a failed verdict says why") {
  ds::Constants bad_marker = ds::kDefaultConstants;
  bad_marker.magic[0] = 'X';
  ds::Constants bad_version = ds::kDefaultConstants;
  bad_version.version = 7;
  CHECK(std::strlen(ds::verify(bad_marker).reason) > 0);
  CHECK(std::strlen(ds::verify(bad_version).reason) > 0);
}

// -- motor flooding damage (ticket 31, ADR-0012) ------------------------------
//
// Both motors take amount*dt while the electric engine room's flood, as read
// this poll, is at or above the threshold and the ordered telegraph is not at
// stop. At any depth; capped at 0.9; never lowered.

namespace {

ds::Constants motor_only(float threshold, float amount) {
  ds::Constants c = ds::kDefaultConstants;
  for (auto& k : c.tier) {
    k.hull_mult = 1.0f;
    k.seep_per_s = 0.0f;
  }
  c.flood_threshold = threshold;
  c.motor_damage_per_s = amount;
  return c;
}

ds::Sample engines(ds::Sample s, float port, float stbd) {
  s.engine_port = port;
  s.engine_stbd = stbd;
  return s;
}

// A sample with the motor room at `flood` and the telegraph at `telegraph`.
ds::Sample motor_room(ds::Sample s, float flood, float telegraph) {
  s.seep_flood = flood;
  s.has_seep = true;
  s.telegraph = telegraph;
  s.has_telegraph = true;
  return s;
}

const ds::Write* write_to(const ds::StepResult& r, ds::Write::Target t) {
  for (const ds::Write& w : r.writes)
    if (w.target == t) return &w;
  return nullptr;
}

// Baselines on `s` at clock 10, then polls the same sample at `clock`.
ds::StepResult poll_twice(const ds::Constants& c, const ds::Sample& s,
                          std::int32_t clock) {
  ds::StepState st{};
  ds::Sample a = s;
  a.clock_s = 10;
  ds::step(c, st, &a);
  ds::Sample later = s;
  later.clock_s = clock;
  return ds::step(c, st, &later);
}

}  // namespace

TEST_CASE("motor: both engines step by amount*dt") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  const ds::Sample s = motor_room(engines(at(120.0f, 500.0f, 0), 0.1f, 0.2f), 0.5f, 1.0f);
  const ds::StepResult r = poll_twice(c, s, 15);  // dt 5
  const ds::Write* port = write_to(r, ds::Write::Target::PortEngine);
  const ds::Write* stbd = write_to(r, ds::Write::Target::StarboardEngine);
  REQUIRE(port);
  REQUIRE(stbd);
  CHECK(port->old_value == doctest::Approx(0.1));
  CHECK(port->value == doctest::Approx(0.15));
  CHECK(stbd->old_value == doctest::Approx(0.2));
  CHECK(stbd->value == doctest::Approx(0.25));
  CHECK(r.writes.size() == 2);
}

TEST_CASE("motor: clamps at 0.9, no write at or above it, never lowers") {
  const ds::Constants c = motor_only(0.3f, 0.1f);
  {
    // A big step clamps to the ceiling exactly.
    const ds::Sample s = motor_room(engines(at(120.0f, 500.0f, 0), 0.85f, 0.0f), 0.5f, 1.0f);
    const ds::StepResult r = poll_twice(c, s, 20);
    const ds::Write* port = write_to(r, ds::Write::Target::PortEngine);
    REQUIRE(port);
    CHECK(port->value == ds::kMotorCeiling);
  }
  {
    // 0.9 read back from a float32 store is 0.89999998: no write. Above it,
    // a higher damage from elsewhere is never lowered. Each engine on its own.
    const float stored = 0.89999998f;
    const ds::Sample s = motor_room(engines(at(120.0f, 500.0f, 0), stored, 0.95f), 0.5f, 1.0f);
    CHECK(poll_twice(c, s, 11).writes.empty());
    const ds::Sample one = motor_room(engines(at(120.0f, 500.0f, 0), stored, 0.4f), 0.5f, 1.0f);
    const ds::StepResult r = poll_twice(c, one, 11);
    CHECK(write_to(r, ds::Write::Target::PortEngine) == nullptr);
    REQUIRE(write_to(r, ds::Write::Target::StarboardEngine));
    CHECK(write_to(r, ds::Write::Target::StarboardEngine)->value == doctest::Approx(0.5));
  }
}

TEST_CASE("motor: never past 0.9 over a long flooded run") {
  const ds::Constants c = motor_only(0.3f, 0.037f);
  ds::StepState st{};
  float port = 0.0f, stbd = 0.0f;
  const ds::Sample first = motor_room(engines(at(120.0f, 500.0f, 0), 0.0f, 0.0f), 0.6f, 0.5f);
  ds::step(c, st, &first);
  for (int t = 1; t <= 500; ++t) {
    const ds::Sample s = motor_room(engines(at(120.0f, 500.0f, t), port, stbd), 0.6f, 0.5f);
    for (const ds::Write& w : ds::step(c, st, &s).writes) {
      CHECK(w.value <= ds::kMotorCeiling);
      CHECK(w.value > w.old_value);
      (w.target == ds::Write::Target::PortEngine ? port : stbd) = w.value;
    }
  }
  CHECK(port == ds::kMotorCeiling);
  CHECK(stbd == ds::kMotorCeiling);
}

TEST_CASE("motor: nothing below the threshold; at it, damage") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  const ds::Sample below = motor_room(at(120.0f, 500.0f, 0), 0.29f, 1.0f);
  CHECK(poll_twice(c, below, 11).writes.empty());
  const ds::Sample on = motor_room(at(120.0f, 500.0f, 0), 0.3f, 1.0f);
  CHECK(poll_twice(c, on, 11).writes.size() == 2);
}

TEST_CASE("motor: nothing at telegraph stop, or with no telegraph value") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  for (float stop : {0.0f, -0.0f}) {
    CAPTURE(stop);
    const ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.6f, stop);
    CHECK(poll_twice(c, s, 11).writes.empty());
  }
  ds::Sample none = motor_room(at(120.0f, 500.0f, 0), 0.6f, 1.0f);
  none.has_telegraph = false;  // a stale 1.0 left in the field
  CHECK(poll_twice(c, none, 11).writes.empty());
}

TEST_CASE("motor: with no telegraph value the other effects still run") {
  ds::Constants c = motor_only(0.3f, 0.01f);
  for (auto& k : c.tier) k.seep_per_s = 0.002f;
  ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.4f, 1.0f);
  s.has_telegraph = false;
  const ds::StepResult r = poll_twice(c, s, 15);
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].target == ds::Write::Target::Flood);
}

TEST_CASE("motor: astern counts as running") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  const ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.6f, -0.5f);
  CHECK(poll_twice(c, s, 11).writes.size() == 2);
}

TEST_CASE("motor: nothing when paused, when the clock goes back, or at amount 0") {
  {
    const ds::Constants c = motor_only(0.3f, 0.01f);
    ds::StepState st{};
    const ds::Sample a = motor_room(at(120.0f, 500.0f, 5), 0.6f, 1.0f);
    ds::step(c, st, &a);
    CHECK(ds::step(c, st, &a).writes.empty());  // dt 0
    const ds::Sample back = motor_room(at(120.0f, 500.0f, 3), 0.6f, 1.0f);
    CHECK(ds::step(c, st, &back).writes.empty());  // dt < 0
  }
  const ds::Constants off = motor_only(0.3f, 0.0f);
  const ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.6f, 1.0f);
  CHECK(poll_twice(off, s, 20).writes.empty());
}

TEST_CASE("motor: acts above 100 m and outside any tier, surfaced too") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  for (float depth : {0.0f, 30.0f, 99.0f}) {
    CAPTURE(depth);
    const ds::Sample s = motor_room(at(depth, 500.0f, 0), 0.6f, 1.0f);
    const ds::StepResult r = poll_twice(c, s, 11);
    CHECK(r.diag.tier == ds::Tier::None);
    CHECK(r.writes.size() == 2);
  }
}

TEST_CASE("motor: acts in tier 2 and below crush depth at the same amount") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  for (float depth : {152.0f, 200.0f}) {
    CAPTURE(depth);
    const ds::Sample s = motor_room(engines(at(depth, 480.0f, 0), 0.0f, 0.0f), 0.6f, 1.0f);
    const ds::StepResult r = poll_twice(c, s, 12);
    REQUIRE(write_to(r, ds::Write::Target::PortEngine));
    CHECK(write_to(r, ds::Write::Target::PortEngine)->value == doctest::Approx(0.02));
  }
}

TEST_CASE("motor: the trigger is the flood read this poll, not after APE's seep write") {
  ds::Constants c = motor_only(0.3f, 0.01f);
  for (auto& k : c.tier) k.seep_per_s = 0.01f;
  // 0.29 read; seepage raises it to 0.34 this poll, but the motors are spared.
  const ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.29f, 1.0f);
  const ds::StepResult r = poll_twice(c, s, 15);
  const ds::Write* flood = write_to(r, ds::Write::Target::Flood);
  REQUIRE(flood);
  CHECK(flood->value > c.flood_threshold);
  CHECK(write_to(r, ds::Write::Target::PortEngine) == nullptr);
  CHECK(write_to(r, ds::Write::Target::StarboardEngine) == nullptr);
}

TEST_CASE("motor: no motor room (no seep compartment) means no motor damage") {
  const ds::Constants c = motor_only(0.3f, 0.01f);
  ds::Sample s = motor_room(at(120.0f, 500.0f, 0), 0.6f, 1.0f);
  s.has_seep = false;
  CHECK(poll_twice(c, s, 11).writes.empty());
}

TEST_CASE("motor: an engine damage outside [0, 1] fails the plausibility gate") {
  ds::Sample s = at(120.0f, 500.0f, 1);
  CHECK(ds::implausible(s) == nullptr);
  s.engine_port = 1.1f;
  CHECK(ds::implausible(s) != nullptr);
  s.engine_port = 0.0f;
  s.engine_stbd = -0.1f;
  CHECK(ds::implausible(s) != nullptr);
}

TEST_CASE("telegraph: the reader's plausibility gate") {
  for (float ok : {0.0f, -0.0f, 0.25f, 1.0f, -1.0f, -0.5f}) {
    CAPTURE(ok);
    CHECK(ds::telegraph_plausible(ok));
  }
  const float inf = std::numeric_limits<float>::infinity();
  for (float bad : {1.01f, -1.01f, 3.0e7f, std::nanf(""), inf, -inf}) {
    CAPTURE(bad);
    CHECK_FALSE(ds::telegraph_plausible(bad));
  }
}

// -- the seep zone (ticket 30, ADR-0012) -------------------------------------
// The reader hands over each zone's compartment id from the zone->compartment
// table, in list order (-1 for none); the seep zone is the first that maps to
// compartment 4, the electric engine room, whatever the class.

TEST_CASE("seep zone: a VII/IX list with compartments 8 and 4 picks 4, not 8") {
  const std::int8_t comps[] = {1, 2, 3, 8, 7, 4, 5, 6};
  CHECK(ds::pick_seep_zone(comps, 8) == 5);
}

TEST_CASE("seep zone: a II/XXI list without compartment 8 picks 4") {
  const std::int8_t comps[] = {-1, 1, 2, 4, 3};
  CHECK(ds::pick_seep_zone(comps, 5) == 3);
}

TEST_CASE("seep zone: the first zone in list order wins") {
  const std::int8_t comps[] = {2, 4, 4};
  CHECK(ds::pick_seep_zone(comps, 3) == 1);
}

TEST_CASE("seep zone: no zone maps to compartment 4, so there is none") {
  const std::int8_t comps[] = {1, 8, -1};
  CHECK(ds::pick_seep_zone(comps, 3) == -1);
  CHECK(ds::pick_seep_zone(comps, 0) == -1);
}

// -- seepage (ticket 25) ------------------------------------------------------

namespace {

ds::Constants seep_only(float rate) {
  ds::Constants c = ds::kDefaultConstants;
  for (auto& k : c.tier) {
    k.hull_mult = 1.0f;
    k.seep_per_s = rate;
  }
  c.motor_damage_per_s = 0.0f;
  return c;
}

ds::Sample flooded(ds::Sample s, float flood) {
  s.seep_flood = flood;
  s.has_seep = true;
  return s;
}

}  // namespace

TEST_CASE("seep: steps by rate*dt") {
  const ds::Constants c = seep_only(0.002f);
  ds::StepState st{};
  const ds::Sample a = flooded(at(120.0f, 500.0f, 10), 0.1f);
  const ds::Sample b = flooded(at(120.0f, 500.0f, 15), 0.1f);
  ds::step(c, st, &a);
  const ds::StepResult r = ds::step(c, st, &b);
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].target == ds::Write::Target::Flood);
  CHECK(r.writes[0].old_value == doctest::Approx(0.1));
  CHECK(r.writes[0].value == doctest::Approx(0.11));
}

TEST_CASE("seep: a level the crew pumped dry is floored, not left at 0") {
  const ds::Constants c = seep_only(0.0005f);  // one step is below the floor
  ds::StepState st{};
  const ds::Sample a = flooded(at(120.0f, 500.0f, 10), 0.0f);
  const ds::Sample b = flooded(at(120.0f, 500.0f, 11), 0.0f);
  ds::step(c, st, &a);
  const ds::StepResult r = ds::step(c, st, &b);
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].value == doctest::Approx(0.016));  // 0.01 + 0.006 * 1 s
  const ds::Sample at_floor = flooded(at(120.0f, 500.0f, 12), 0.016f);
  const ds::StepResult r2 = ds::step(c, st, &at_floor);
  REQUIRE(r2.writes.size() == 1);
  CHECK(r2.writes[0].value > 0.016f);  // still only ever raised
}

TEST_CASE("seep: the floor grows with dt under time compression, and is capped") {
  const ds::Constants c = seep_only(0.0005f);
  {
    ds::StepState st{};
    const ds::Sample a = flooded(at(120.0f, 500.0f, 10), 0.0f);
    const ds::Sample b = flooded(at(120.0f, 500.0f, 17), 0.0f);  // dt 7
    ds::step(c, st, &a);
    const ds::StepResult r = ds::step(c, st, &b);
    REQUIRE(r.writes.size() == 1);
    CHECK(r.writes[0].value == doctest::Approx(0.052));
  }
  {
    ds::StepState st{};
    const ds::Sample a = flooded(at(120.0f, 500.0f, 10), 0.0f);
    const ds::Sample b = flooded(at(120.0f, 500.0f, 510), 0.0f);  // dt 500
    ds::step(c, st, &a);
    const ds::StepResult r = ds::step(c, st, &b);
    REQUIRE(r.writes.size() == 1);
    CHECK(r.writes[0].value == doctest::Approx(ds::kSeepFloorMax));
  }
}

TEST_CASE("seep: clamps at 0.8, then writes nothing, and never lowers") {
  const ds::Constants c = seep_only(0.05f);
  ds::StepState st{};
  const ds::Sample a = flooded(at(120.0f, 500.0f, 0), 0.78f);
  ds::step(c, st, &a);
  const ds::Sample b = flooded(at(120.0f, 500.0f, 10), 0.78f);
  const ds::StepResult r = ds::step(c, st, &b);
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].value == ds::kSeepCeiling);

  const ds::Sample at_cap = flooded(at(120.0f, 500.0f, 20), ds::kSeepCeiling);
  CHECK(ds::step(c, st, &at_cap).writes.empty());
  const ds::Sample above = flooded(at(120.0f, 500.0f, 30), 0.95f);
  CHECK(ds::step(c, st, &above).writes.empty());  // a bigger leak elsewhere stays
}

TEST_CASE("seep: none above 100 m, when paused, or when the clock goes back") {
  const ds::Constants c = seep_only(0.01f);
  {
    ds::StepState st{};
    const ds::Sample a = flooded(at(60.0f, 500.0f, 1), 0.0f);
    const ds::Sample b = flooded(at(60.0f, 500.0f, 9), 0.0f);
    ds::step(c, st, &a);
    CHECK(ds::step(c, st, &b).writes.empty());
  }
  {
    ds::StepState st{};
    const ds::Sample a = flooded(at(120.0f, 500.0f, 5), 0.0f);
    ds::step(c, st, &a);
    CHECK(ds::step(c, st, &a).writes.empty());  // dt 0
    const ds::Sample back = flooded(at(120.0f, 500.0f, 3), 0.0f);
    CHECK(ds::step(c, st, &back).writes.empty());  // dt < 0
  }
}

TEST_CASE("seep: leakage 0 turns it off without touching the other effects") {
  ds::Constants c = seep_only(0.0f);
  for (auto& k : c.tier) k.hull_mult = 2.0f;
  ds::StepState st{};
  const ds::Sample a = flooded(at(120.0f, 500.0f, 1), 0.0f);
  ds::step(c, st, &a);
  const ds::Sample b = flooded(at(120.0f, 480.0f, 2), 0.0f);  // 20 HP drop
  const ds::StepResult r = ds::step(c, st, &b);
  REQUIRE(r.writes.size() == 1);
  CHECK(r.writes[0].target == ds::Write::Target::Hull);
}

TEST_CASE("seep: continues at tier-2 values, below crush depth too") {
  ds::Constants c = seep_only(0.002f);
  c.tier[1].seep_per_s = 0.0025f;
  for (float depth : {152.0f, 200.0f}) {  // tier 2; tier 3 (onset 154.8 at 480)
    ds::StepState st{};
    const ds::Sample a = flooded(at(depth, 480.0f, 10), 0.2f);
    const ds::Sample b = flooded(at(depth, 480.0f, 20), 0.2f);
    ds::step(c, st, &a);
    const ds::StepResult r = ds::step(c, st, &b);
    CAPTURE(depth);
    REQUIRE(r.writes.size() == 1);
    CHECK(r.writes[0].value == doctest::Approx(0.225));
  }
}

TEST_CASE("seep: a sample with no seep compartment makes no seep write") {
  const ds::Constants c = seep_only(0.01f);
  ds::StepState st{};
  ds::Sample a = at(120.0f, 500.0f, 1);
  ds::Sample b = at(120.0f, 500.0f, 9);
  a.seep_flood = b.seep_flood = 0.1f;  // a stale value, has_seep false
  ds::step(c, st, &a);
  CHECK(ds::step(c, st, &b).writes.empty());
}

TEST_CASE("seep: a flood outside [0, 1] fails the plausibility gate") {
  ds::Sample s = flooded(at(120.0f, 500.0f, 1), 0.5f);
  CHECK(ds::implausible(s) == nullptr);
  s.seep_flood = 1.2f;
  CHECK(ds::implausible(s) != nullptr);
  s.seep_flood = -0.1f;
  CHECK(ds::implausible(s) != nullptr);
}

TEST_CASE("seep: a seep write and motor writes can share a poll") {
  ds::Constants c = seep_only(0.01f);
  c.flood_threshold = 0.3f;
  c.motor_damage_per_s = 0.05f;
  ds::StepState st{};
  const ds::Sample a = motor_room(at(120.0f, 500.0f, 1), 0.4f, 1.0f);
  const ds::Sample b = motor_room(at(120.0f, 500.0f, 2), 0.4f, 1.0f);
  ds::step(c, st, &a);
  const ds::StepResult r = ds::step(c, st, &b);
  CHECK(write_to(r, ds::Write::Target::Flood) != nullptr);
  CHECK(write_to(r, ds::Write::Target::PortEngine) != nullptr);
  CHECK(write_to(r, ds::Write::Target::StarboardEngine) != nullptr);
}
