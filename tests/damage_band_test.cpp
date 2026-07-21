#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "doctest.h"

#include "core/damage_band.hpp"

using ape::Band;
using ape::BandConfig;
using ape::select_band;

namespace {
// Reference config uses binary-exact fractions so that "exactly on a band
// boundary" is a deterministic test rather than a floating-point coin flip.
// Cumulative edges: light [0, 1/8), medium [1/8, 1/4), heavy [1/4, +inf).
// (1/8, 1/4, and 1/2 are all binary-exact; 1/2 sits well inside heavy.)
constexpr BandConfig kCfg{
    /*light_window=*/0.125, /*medium_window=*/0.125,
    /*light_rate=*/1.0, /*medium_rate=*/3.0, /*heavy_rate=*/9.0};

// Depth sitting `fraction` under a crush depth of 100. With a binary-exact
// fraction, (depth - 100)/100 recovers `fraction` exactly.
double depth_at(double fraction) { return 100.0 + fraction * 100.0; }
}  // namespace

TEST_CASE("depth within each band selects that band and rate") {
  CHECK(select_band(depth_at(0.0625), 100.0, kCfg).band == Band::Light);   // 1/16
  CHECK(select_band(depth_at(0.0625), 100.0, kCfg).rate == doctest::Approx(1.0));

  CHECK(select_band(depth_at(0.1875), 100.0, kCfg).band == Band::Medium);  // 3/16
  CHECK(select_band(depth_at(0.1875), 100.0, kCfg).rate == doctest::Approx(3.0));

  CHECK(select_band(depth_at(0.375), 100.0, kCfg).band == Band::Heavy);    // 3/8
  CHECK(select_band(depth_at(0.375), 100.0, kCfg).rate == doctest::Approx(9.0));
}

TEST_CASE("band boundaries belong to the upper band (half-open intervals)") {
  // 0.125 edge -> medium, not light
  CHECK(select_band(depth_at(0.125), 100.0, kCfg).band == Band::Medium);
  // 0.25 edge -> heavy, not medium
  CHECK(select_band(depth_at(0.25), 100.0, kCfg).band == Band::Heavy);
}

TEST_CASE("far below crush depth stays in the open-ended heavy band") {
  // The heavy band has no outer edge: past the medium edge, heavy always
  // applies and returns the heavy rate (never a "defer"/zero rate).
  auto at_edge = select_band(depth_at(0.5), 100.0, kCfg);   // 1/2 under
  CHECK(at_edge.band == Band::Heavy);
  CHECK(at_edge.rate == doctest::Approx(9.0));

  auto deep = select_band(depth_at(10.0), 100.0, kCfg);     // 10x crush depth under
  CHECK(deep.band == Band::Heavy);
  CHECK(deep.rate == doctest::Approx(9.0));
}

TEST_CASE("exactly at crush depth (zero percent under) is the light band") {
  auto r = select_band(100.0, 100.0, kCfg);
  CHECK(r.band == Band::Light);
  CHECK(r.rate == doctest::Approx(1.0));
}

TEST_CASE("a zero-width band is skipped, not selectable") {
  BandConfig cfg = kCfg;
  cfg.medium_window = 0.0;  // medium collapses; light [0,0.125), heavy [0.125,+inf)
  CHECK(select_band(depth_at(0.125), 100.0, cfg).band == Band::Heavy);
  CHECK(select_band(depth_at(0.1875), 100.0, cfg).band == Band::Heavy);
}
