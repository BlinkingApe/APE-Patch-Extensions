#ifndef APE_MODS_DEPTH_STRESS_WARNING_HPP
#define APE_MODS_DEPTH_STRESS_WARNING_HPP

// The motor-room flooding warning (ticket 35, from ticket 34's grilling).
//
// APE never calls a stock function (ADR-0001), so the warning borrows stock's
// own flooding report: APE swaps the report string's pointer for its own text,
// clears the motor room's latch so the game speaks, then puts the pointer
// back. This file is the pure decision half: when to swap, which band, when to
// restore or give up. The memory reads and writes are in ape_act.

#include <cstdint>

#include "mods/depth_stress/constants.hpp"
#include "mods/depth_stress/step.hpp"

namespace ape {
namespace depth_stress {

// What the chief says. English only. It is stock's report text, so it goes
// through sprintf as a format string: it must never contain '%'. The report
// buffer holds at most 0x130 bytes. The game prefixes "CE: " itself.
constexpr char kWarningText[] =
    "Water in the motor room, sir! Pump it out or stop the motors!";

// The warning level sits this far below the flooding threshold, never under
// the floor; it re-arms this far below the warning level.
constexpr float kWarningBelowThreshold = 0.05f;
constexpr float kWarningFloor = 0.01f;
constexpr float kWarningRearmGap = 0.05f;
// How long the swap may wait for the game to speak, in real milliseconds.
constexpr std::uint32_t kWarningTimeoutMs = 10000;

// The motor-room flood fraction at which the warning fires.
float warning_level(float flood_threshold);

// Stock's three flooding bands, by the room's flood: 1 below 0.35, 2 from 0.35
// below 0.60, 3 from 0.60. Each has its own report string (592, 593, 594) and
// latch, so the warning borrows the one the level is in.
int warning_band(float flood);

enum class WarningAction {
  None,
  Swap,     // swap in APE's text for `band`'s string and clear its latch
  Restore,  // the game spoke: put the original string back
  GiveUp,   // it didn't within the timeout: put it back, crossing spent
};

struct WarningDecision {
  WarningAction action = WarningAction::None;
  int band = 0;  // for Swap, Restore and GiveUp: the band swapped
};

// What the warning carries between polls. Value-initialise one and hand it
// back each poll.
struct WarningState {
  bool armed = true;     // may fire on the next crossing
  bool swapped = false;  // APE's pointer is in the table now
  int band = 0;          // the band swapped
  std::uint32_t swapped_at_ms = 0;
  bool has_boat = false;
  std::uint32_t boat = 0;
};

struct WarningInput {
  // The poll's sample, null when there is none. It is trusted: the caller has
  // already applied implausible().
  const Sample* sample = nullptr;
  std::uint32_t now_ms = 0;  // real time, GetTickCount
  // Only read while swapped: the swapped band's latch for the motor room now
  // reads set (the game has run its report).
  bool latch_set = false;
};

WarningDecision decide_warning(const Constants& c, WarningState& st,
                               const WarningInput& in);

}  // namespace depth_stress
}  // namespace ape

#endif  // APE_MODS_DEPTH_STRESS_WARNING_HPP
