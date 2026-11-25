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

TEST_CASE("a failed verdict says why") {
  ds::Constants bad_marker = ds::kDefaultConstants;
  bad_marker.magic[0] = 'X';
  ds::Constants bad_version = ds::kDefaultConstants;
  bad_version.version = 7;
  CHECK(std::strlen(ds::verify(bad_marker).reason) > 0);
  CHECK(std::strlen(ds::verify(bad_version).reason) > 0);
}
