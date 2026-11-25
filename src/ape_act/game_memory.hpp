#ifndef APE_APE_ACT_GAME_MEMORY_HPP
#define APE_APE_ACT_GAME_MEMORY_HPP

// The game-memory reader and writer (PRD Modules). Windows-only: everything
// here reads or writes the host process, through calls that fail rather than
// fault on a bad pointer.
//
// Every address is reached from an AOB-anchored root and a recorded chain
// (ADR-0003; the anchors and chains are in docs/re-notes/09-depth-stress-
// runtime-anchors.md). An anchor that doesn't resolve, a null link, a failed
// read or a failed plausibility gate means no sample, with a reason.

#include <cstdint>

#include "mods/depth_stress/step.hpp"

namespace ape {
namespace game {

// The absolute roots the signatures recover. Resolved once, at loop start.
struct Anchors {
  std::uint32_t boat_root = 0;   // 0x00554A88 on OAE 1.5.2
  std::uint32_t depth_root = 0;  // 0x006307BC
  std::uint32_t clock = 0;       // 0x0063FF80, h.sie's mirror
  // Null when all three resolved; else which signature didn't, and why.
  const char* failed = nullptr;
  const char* why = nullptr;
};

// Scans sh3.exe's executable sections; each signature must match exactly once.
Anchors resolve_anchors();

// One poll's sample. False, with `why`, when there is none this poll.
bool read_sample(const Anchors& a, depth_stress::Sample& out, const char** why);

// Applies the step's hull write to the boat the same poll's sample named.
bool write_hull(std::uint32_t boat, float value);

}  // namespace game
}  // namespace ape

#endif  // APE_APE_ACT_GAME_MEMORY_HPP
