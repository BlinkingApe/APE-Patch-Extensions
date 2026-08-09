// config_check — manual verification harness for the ape.cfg loader.
//
// Per the spec's testing decision (PRD "Testing Decisions"), config-file
// parsing is deliberately NOT part of the automated unit-test seam. This small
// standalone harness is how issue 04 is verified instead:
//
//   1. Self-check: parse an embedded sample with known values and assert every
//      BandConfig field matches. Exits non-zero on any mismatch.
//   2. If a path argument is given, load that real file and print the parsed
//      values so the shipped data/Cfg/ape.cfg can be eyeballed.
//
// It is intentionally not registered with ctest — run it by hand.

#include <cstdio>
#include <sstream>
#include <string>

#include "mods/crush_depth_damage/config.hpp"
#include "mods/crush_depth_damage/damage_band.hpp"

namespace {

int failures = 0;

void expect_eq(const char* what, double got, double want) {
  const bool ok = got == want;
  if (!ok) ++failures;
  std::printf("  [%s] %-20s got %g want %g\n", ok ? "ok" : "FAIL", what, got,
              want);
}

void print_config(const ape::BandConfig& c) {
  std::printf("  light : window=%g rate=%g\n", c.light_window, c.light_rate);
  std::printf("  medium: window=%g rate=%g\n", c.medium_window, c.medium_rate);
  std::printf("  heavy : (open-ended) rate=%g\n", c.heavy_rate);
}

// Self-check against an embedded sample exercising comments, blank lines,
// case-insensitive keys, trailing comments, and an unrelated section.
int self_check() {
  const char* sample =
      "; a comment\n"
      "\n"
      "[SomeOtherHack]\n"
      "light_damage_window = 999   ; must be ignored (wrong section)\n"
      "\n"
      "[CrushDepthDamage]\n"
      "light_damage_window  = 0.05   ; trailing comment\n"
      "LIGHT_DAMAGE_RATE    = 0.5    # case-insensitive key\n"
      "medium_damage_window = 0.10\n"
      "medium_damage_rate   = 2.0\n"
      "heavy_damage_rate    = 6.0\n"
      "unknown_future_key   = 42     ; ignored, not an error\n";

  std::istringstream in(sample);
  ape::BandConfig cfg{};
  std::string error;
  std::puts("self-check (embedded sample):");
  if (!ape::parse_band_config(in, cfg, error)) {
    std::printf("  FAIL parse returned false: %s\n", error.c_str());
    return 1;
  }
  expect_eq("light_window", cfg.light_window, 0.05);
  expect_eq("light_rate", cfg.light_rate, 0.5);
  expect_eq("medium_window", cfg.medium_window, 0.10);
  expect_eq("medium_rate", cfg.medium_rate, 2.0);
  expect_eq("heavy_rate", cfg.heavy_rate, 6.0);

  // A file missing a required key must be rejected with a clear message.
  std::istringstream missing("[CrushDepthDamage]\nlight_damage_window = 0.05\n");
  ape::BandConfig ignore{};
  std::string missing_err;
  if (ape::parse_band_config(missing, ignore, missing_err)) {
    std::puts("  [FAIL] incomplete section was accepted");
    ++failures;
  } else {
    std::printf("  [ok] incomplete section rejected: %s\n",
                missing_err.c_str());
  }
  return failures == 0 ? 0 : 1;
}

}  // namespace

int main(int argc, char** argv) {
  const int rc = self_check();

  if (argc >= 2) {
    std::printf("\nloading real file: %s\n", argv[1]);
    ape::BandConfig cfg{};
    std::string error;
    if (ape::load_band_config(argv[1], cfg, error)) {
      print_config(cfg);
    } else {
      std::printf("  FAIL: %s\n", error.c_str());
      return 1;
    }
  }

  std::printf("\n%s\n", rc == 0 ? "self-check PASSED" : "self-check FAILED");
  return rc;
}
