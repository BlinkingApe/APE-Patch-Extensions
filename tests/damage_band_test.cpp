#define DOCTEST_CONFIG_IMPLEMENT_WITH_MAIN
#include "doctest.h"

#include "core/damage_band.hpp"

using ape::Band;
using ape::BandConfig;
using ape::select_band;

namespace {
// Reference config uses binary-exact fractions (1/8, 1/8, 1/4) so that
// "exactly on a band boundary" is a deterministic test rather than a
// floating-point coin flip. Cumulative edges: light 0.125, medium 0.25,
// heavy 0.5 (fraction under crush depth).
constexpr BandConfig kCfg{
    /*light_window=*/0.125, /*medium_window=*/0.125, /*heavy_window=*/0.25,
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

TEST_CASE("past the heavy edge defers to stock behavior") {
  auto r = select_band(depth_at(0.5), 100.0, kCfg);  // 0.5 == heavy upper edge
  CHECK(r.band == Band::PastHeavy);
  CHECK(r.defer_to_stock == true);
  CHECK(r.rate == doctest::Approx(0.0));

  CHECK(select_band(depth_at(0.75), 100.0, kCfg).defer_to_stock == true);
}

TEST_CASE("exactly at crush depth (zero percent under) is the light band") {
  auto r = select_band(100.0, 100.0, kCfg);
  CHECK(r.band == Band::Light);
  CHECK(r.defer_to_stock == false);
}

TEST_CASE("a zero-width band is skipped, not selectable") {
  BandConfig cfg = kCfg;
  cfg.medium_window = 0.0;  // medium collapses; light [0,0.125), heavy [0.125,0.375)
  CHECK(select_band(depth_at(0.125), 100.0, cfg).band == Band::Heavy);
  CHECK(select_band(depth_at(0.1875), 100.0, cfg).band == Band::Heavy);
}
