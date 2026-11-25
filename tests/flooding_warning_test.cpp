// Ticket 35: the pure decision half of the motor-room flooding warning, under
// vendored doctest. The main() lives in depth_stress_test.cpp.

#include "doctest.h"

#include <cstring>

#include "mods/depth_stress/warning.hpp"

namespace ds = ape::depth_stress;

TEST_CASE("warning text is safe to hand the game's sprintf") {
  // Stock formats the string with no arguments: a '%' would be read as one.
  CHECK(std::strchr(ds::kWarningText, '%') == nullptr);
  // The report buffer is at most 0x130 bytes, NUL included.
  CHECK(sizeof ds::kWarningText <= 0x130);
  // The game adds "CE: " from the speaker.
  CHECK(std::strncmp(ds::kWarningText, "CE:", 3) != 0);
}

TEST_CASE("warning level: 5 points under the threshold, never under 1 %") {
  CHECK(ds::warning_level(0.20f) == doctest::Approx(0.15));
  CHECK(ds::warning_level(0.50f) == doctest::Approx(0.45));
  CHECK(ds::warning_level(0.95f) == doctest::Approx(0.90));
  // The patcher's minimum threshold is 5 %: the floor holds there.
  CHECK(ds::warning_level(0.05f) == doctest::Approx(0.01));
  CHECK(ds::warning_level(0.03f) == doctest::Approx(0.01));
}

// -- the decision ------------------------------------------------------------

namespace {

// A plausible sample with the motors running ahead and the room at `flood`.
ds::Sample sample(float flood, float telegraph = 1.0f) {
  ds::Sample s{};
  s.boat = 0x1000;
  s.clock_s = 1000;
  s.depth_m = 50.0f;
  s.hull = 400.0f;
  s.max_hull = 450.0f;
  s.crush_field = 20.0f;
  s.crush_speed = 0.05f;
  s.seep_flood = flood;
  s.has_seep = true;
  s.telegraph = telegraph;
  s.has_telegraph = true;
  return s;
}

ds::WarningInput poll(const ds::Sample& s, std::uint32_t now_ms = 0,
                      bool latch_set = false) {
  ds::WarningInput in;
  in.sample = &s;
  in.now_ms = now_ms;
  in.latch_set = latch_set;
  return in;
}

}  // namespace

TEST_CASE("bands follow stock's report: below 0.35, to 0.60, from 0.60") {
  CHECK(ds::warning_band(0.01f) == 1);
  CHECK(ds::warning_band(0.34f) == 1);
  CHECK(ds::warning_band(0.35f) == 2);
  CHECK(ds::warning_band(0.59f) == 2);
  CHECK(ds::warning_band(0.60f) == 3);
  CHECK(ds::warning_band(0.95f) == 3);
}

TEST_CASE("warning: the first poll at the warning level with motors running swaps") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.15f);
  const ds::WarningDecision d =
      ds::decide_warning(ds::kDefaultConstants, st, poll(s));
  CHECK(d.action == ds::WarningAction::Swap);
  CHECK(d.band == 1);
}

TEST_CASE("warning: the band swapped is the one the level is in") {
  // A threshold of 0.50 puts the level at 0.45, in band 2; 0.70 puts it at
  // 0.65, in band 3.
  ds::Constants c = ds::kDefaultConstants;
  c.flood_threshold = 0.50f;
  ds::WarningState st2{};
  const ds::Sample s2 = sample(0.45f);
  CHECK(ds::decide_warning(c, st2, poll(s2)).band == 2);
  c.flood_threshold = 0.70f;
  ds::WarningState st3{};
  const ds::Sample s3 = sample(0.65f);
  CHECK(ds::decide_warning(c, st3, poll(s3)).band == 3);
}

TEST_CASE("warning: nothing below the warning level") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.14f);
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::None);
}

TEST_CASE("warning: nothing while the telegraph is at stop, or unknown") {
  ds::WarningState st{};
  ds::Sample s = sample(0.30f, 0.0f);
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::None);
  s = sample(0.30f, -0.0f);  // -0.0 is stop too
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::None);
  s = sample(0.30f);
  s.has_telegraph = false;
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::None);
}

TEST_CASE("warning: astern counts as running") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f, -0.4f);
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::Swap);
}

TEST_CASE("warning: nothing with no motor-room reading, or no sample") {
  ds::WarningState st{};
  ds::Sample s = sample(0.30f);
  s.has_seep = false;
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s)).action ==
        ds::WarningAction::None);
  ds::WarningInput none;
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, none).action ==
        ds::WarningAction::None);
}

TEST_CASE("warning: off when motor damage is off (amount 0)") {
  ds::Constants c = ds::kDefaultConstants;
  c.motor_damage_per_s = 0.0f;
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  CHECK(ds::decide_warning(c, st, poll(s)).action == ds::WarningAction::None);
}

// -- the swap's life: wait, restore, or give up --------------------------------

TEST_CASE("swap: waits for the game, and never swaps twice") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1000)).action ==
        ds::WarningAction::Swap);
  // The latch isn't set yet: still waiting, nothing more to write.
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1100)).action ==
        ds::WarningAction::None);
  CHECK(st.swapped);
}

TEST_CASE("swap: the first poll that sees the latch set restores the string") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1000));
  const ds::WarningDecision d =
      ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1100, true));
  CHECK(d.action == ds::WarningAction::Restore);
  CHECK(d.band == 1);
  CHECK_FALSE(st.swapped);
  // Spent: the water is still above the level, and it stays quiet.
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1200, true)).action ==
        ds::WarningAction::None);
}

TEST_CASE("swap: restores the band it swapped, even if the level moved") {
  ds::WarningState st{};
  const ds::Sample low = sample(0.30f);
  ds::decide_warning(ds::kDefaultConstants, st, poll(low, 1000));
  const ds::Sample high = sample(0.70f);  // now in band 3
  const ds::WarningDecision d =
      ds::decide_warning(ds::kDefaultConstants, st, poll(high, 1100, true));
  CHECK(d.action == ds::WarningAction::Restore);
  CHECK(d.band == 1);
}

TEST_CASE("swap: gives up at the timeout, once, and doesn't retry the crossing") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  ds::decide_warning(ds::kDefaultConstants, st, poll(s, 5000));
  CHECK(ds::decide_warning(
            ds::kDefaultConstants, st,
            poll(s, 5000 + ds::kWarningTimeoutMs - 1)).action ==
        ds::WarningAction::None);
  const ds::WarningDecision d = ds::decide_warning(
      ds::kDefaultConstants, st, poll(s, 5000 + ds::kWarningTimeoutMs));
  CHECK(d.action == ds::WarningAction::GiveUp);
  CHECK(d.band == 1);
  CHECK_FALSE(st.swapped);
  CHECK(ds::decide_warning(ds::kDefaultConstants, st,
                           poll(s, 5000 + 2 * ds::kWarningTimeoutMs)).action ==
        ds::WarningAction::None);
}

TEST_CASE("swap: the timeout still runs with no sample, and across a clock wrap") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  const std::uint32_t t0 = 0xFFFFF000u;  // GetTickCount wraps at 2^32
  ds::decide_warning(ds::kDefaultConstants, st, poll(s, t0));
  ds::WarningInput none;
  none.now_ms = t0 + ds::kWarningTimeoutMs;  // wraps past zero
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, none).action ==
        ds::WarningAction::GiveUp);
}

TEST_CASE("swap: with no sample, a set latch still restores") {
  ds::WarningState st{};
  const ds::Sample s = sample(0.30f);
  ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1000));
  ds::WarningInput none;
  none.now_ms = 1100;
  none.latch_set = true;
  CHECK(ds::decide_warning(ds::kDefaultConstants, st, none).action ==
        ds::WarningAction::Restore);
}

// -- once per crossing, and what re-arms it ------------------------------------

namespace {

// Runs one warning through to a restored, spent crossing at `flood`.
void spend_one(ds::WarningState& st, float flood) {
  const ds::Sample s = sample(flood);
  REQUIRE(ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1000)).action ==
          ds::WarningAction::Swap);
  REQUIRE(ds::decide_warning(ds::kDefaultConstants, st, poll(s, 1100, true)).action ==
          ds::WarningAction::Restore);
}

ds::WarningAction act(ds::WarningState& st, const ds::Sample& s,
                      const ds::Constants& c = ds::kDefaultConstants) {
  return ds::decide_warning(c, st, poll(s, 2000)).action;
}

}  // namespace

TEST_CASE("rearm: hovering around the level doesn't repeat the warning") {
  ds::WarningState st{};
  spend_one(st, 0.15f);
  // The crew pumps it a little, then it climbs back: still inside the 5-point gap.
  CHECK(act(st, sample(0.11f)) == ds::WarningAction::None);
  CHECK(act(st, sample(0.16f)) == ds::WarningAction::None);
  CHECK(act(st, sample(0.30f)) == ds::WarningAction::None);
}

TEST_CASE("rearm: below the level by more than 5 points warns again on the next rise") {
  ds::WarningState st{};
  spend_one(st, 0.15f);
  CHECK(act(st, sample(0.09f)) == ds::WarningAction::None);
  CHECK(act(st, sample(0.15f)) == ds::WarningAction::Swap);
}

TEST_CASE("rearm: ordering stop, then ahead with the water still above, warns again") {
  ds::WarningState st{};
  spend_one(st, 0.30f);
  CHECK(act(st, sample(0.30f, 0.0f)) == ds::WarningAction::None);
  CHECK(act(st, sample(0.30f, 0.5f)) == ds::WarningAction::Swap);
}

TEST_CASE("rearm: a dry room re-arms even when the level's 1 % floor is in play") {
  ds::Constants c = ds::kDefaultConstants;
  c.flood_threshold = 0.05f;  // warning level 0.01; level - 5 points is negative
  ds::WarningState st{};
  const ds::Sample wet = sample(0.02f);
  REQUIRE(ds::decide_warning(c, st, poll(wet, 1000)).action ==
          ds::WarningAction::Swap);
  REQUIRE(ds::decide_warning(c, st, poll(wet, 1100, true)).action ==
          ds::WarningAction::Restore);
  CHECK(act(st, sample(0.02f), c) == ds::WarningAction::None);
  CHECK(act(st, sample(0.0f), c) == ds::WarningAction::None);
  CHECK(act(st, sample(0.02f), c) == ds::WarningAction::Swap);
}

TEST_CASE("rearm: a new boat (a load) starts the warning afresh") {
  ds::WarningState st{};
  spend_one(st, 0.30f);
  ds::Sample loaded = sample(0.30f);
  loaded.boat = 0x2000;
  CHECK(act(st, loaded) == ds::WarningAction::Swap);
}

TEST_CASE("rearm: a spent crossing stays spent while there's no reading") {
  ds::WarningState st{};
  spend_one(st, 0.30f);
  ds::Sample s = sample(0.30f);
  s.has_seep = false;  // a poll with no motor-room reading changes nothing
  CHECK(act(st, s) == ds::WarningAction::None);
  CHECK(act(st, sample(0.30f)) == ds::WarningAction::None);
}
