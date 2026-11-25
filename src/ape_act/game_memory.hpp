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
  std::uint32_t engine_root = 0;       // 0x005543DC, the engine singleton
  std::uint32_t engine_block_off = 0;  // 0x37C, the electric block's pair
  std::uint32_t zone_head = 0;   // 0x005547D4, head of the zone-state list
  std::uint32_t zone_table = 0;  // 0x00566898, zone -> compartment (signed bytes)
  // Null when all of the above resolved; else which signature didn't, and why.
  const char* failed = nullptr;
  const char* why = nullptr;
  // Optional: 0 when its signature didn't resolve (then `telegraph_why` says
  // why). That costs motor damage only, never the sample.
  std::uint32_t telegraph = 0;   // 0x0063E1D4, h.sie's ordered-telegraph copy
  const char* telegraph_why = nullptr;
};

// Scans sh3.exe's executable sections; each signature must match exactly once.
Anchors resolve_anchors();

// Where the writer puts the step's writes, resolved in the same poll as the
// sample that produced them.
struct Targets {
  std::uint32_t boat = 0;
  std::uint32_t engine_port = 0;  // address of the port engine's damage float
  std::uint32_t engine_stbd = 0;
  std::uint32_t seep_flood = 0;  // address of the seep compartment's flood float
};

// One poll's sample. False, with `why`, when there is none this poll.
bool read_sample(const Anchors& a, depth_stress::Sample& out, Targets& targets,
                 const char** why);

// Applies the step's hull write to the boat the same poll's sample named.
bool write_hull(std::uint32_t boat, float value);

// Applies the step's motor flooding damage write to an engine's damage float.
bool write_engine(std::uint32_t address, float value);

// Applies the step's seepage write to the seep compartment's flood float.
bool write_flood(std::uint32_t address, float value);

// Why the last read_sample found no seep compartment (null when it found one).
// Read from the same thread that polled; the text is static.
const char* seep_unavailable();

// Why the last read_sample had no telegraph value (null when it had one).
// Same thread and lifetime rules as seep_unavailable().
const char* telegraph_unavailable();

}  // namespace game
}  // namespace ape

#endif  // APE_APE_ACT_GAME_MEMORY_HPP
