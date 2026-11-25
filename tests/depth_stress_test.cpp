// Seam 1 (PRD Testing Decisions): the pure depth-stress code, under vendored
// doctest. No Windows headers, no game memory.

#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "doctest.h"

#include <cstring>

#include "mods/depth_stress/constants.hpp"
#include "mods/depth_stress/step.hpp"

namespace ds = ape::depth_stress;

TEST_CASE("the shipped constants carry ticket 12's defaults in runtime units") {
  const ds::Constants c = ds::kDefaultConstants;
  // Patcher conversions (PRD Fields): leakage p %/min -> p/6000 per game-s,
  // wear rate m min -> 1/(60*m) per game-s, wear amount a % -> a/100.
  CHECK(c.tier2_depth_m == doctest::Approx(150.0));
  CHECK(c.tier[0].seep_per_s == doctest::Approx(4.0 / 6000.0));
  CHECK(c.tier[1].seep_per_s == doctest::Approx(15.0 / 6000.0));
  CHECK(c.tier[0].wear_amount == doctest::Approx(0.075));
  CHECK(c.tier[1].wear_amount == doctest::Approx(0.225));
  CHECK(c.tier[0].wear_chance_per_s == doctest::Approx(1.0 / 12000.0));
  CHECK(c.tier[1].wear_chance_per_s == doctest::Approx(1.0 / 7800.0));
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
  c.version = 2;
  CHECK_FALSE(ds::verify(c).ok);
  c.version = 0;
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
