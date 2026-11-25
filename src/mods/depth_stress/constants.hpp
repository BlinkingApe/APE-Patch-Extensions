#ifndef APE_MODS_DEPTH_STRESS_CONSTANTS_HPP
#define APE_MODS_DEPTH_STRESS_CONSTANTS_HPP

// Depth stress's constants struct (ADR-0006).
//
// One instance lives in ape.act's own .data (src/ape_act/depth_stress_
// constants.cpp). The patcher finds it at the file offset the build records
// in the manifest (tools/re-static/gen_manifest.py), checks the marker and
// version, and overwrites the fields with the player's numbers. ape.act checks
// the same marker and version at startup and fails inert on a mismatch.
//
// Every field is float32 in the units the runtime uses; the patcher does the
// GUI-to-runtime conversions (PRD Fields), so nothing is converted per poll.
// The layout is a contract with the patcher: change it only together with
// kVersion.

#include <cstddef>
#include <cstdint>

namespace ape {
namespace depth_stress {

// The marker: 8 bytes, then a version. gen_manifest.py searches the built
// ape.act for this exact run and requires it to occur exactly once.
constexpr char kMagic[8] = {'A', 'P', 'E', 'D', 'P', 'T', 'H', '1'};
// Version 2 (ticket 31, ADR-0012): per-tier wear gave way to one flooding
// threshold and one motor damage amount.
constexpr std::uint32_t kVersion = 2;

// Hard ceilings, fixed here and never part of the struct (PRD user story 47):
// seepage alone never drowns the room's crew, and motor flooding damage never
// leaves a motor the crew can't repair.
constexpr float kSeepCeiling = 0.8f;
// While seeping, the level never rests below a floor. The crew pumps a
// compartment to exactly 0 (about 0.0033 per game-second, measured in play), and
// stock re-arms its "We have flooding, sir!" report only at exactly 0 (ticket 28).
// Under time compression a poll spans several game-seconds, so the floor grows
// with the poll's dt: base + per-second * dt, capped below the 0.35 report band.
// Not part of the struct.
constexpr float kSeepFloor = 0.01f;
constexpr float kSeepFloorPerS = 0.006f;  // about twice the measured pump rate
constexpr float kSeepFloorMax = 0.3f;
constexpr float kMotorCeiling = 0.9f;

// Tier 1 starts at this depth whatever the class; it is not a field.
constexpr float kTier1DepthM = 100.0f;

struct TierConstants {
  float seep_per_s;  // flood fraction added per game-second (0 = off)
  float hull_mult;   // hull damage multiplier (1.0 = off)
};

struct Constants {
  char magic[8];
  std::uint32_t version;
  float tier2_depth_m;
  // Motor flooding damage (ADR-0012): one of each, not per tier.
  float flood_threshold;     // motor-room flood fraction at which it starts
  float motor_damage_per_s;  // added to each motor per game-second (0 = off)
  TierConstants tier[2];  // [0] tier 1, [1] tier 2 (and tier 3)
};

// The layout gen_manifest.py records for the patcher (DEPTH_STRESS_FIELDS).
// A field moved here without moving it there fails the build.
static_assert(sizeof(TierConstants) == 8, "patcher contract: tier is 8 bytes");
static_assert(sizeof(Constants) == 40, "patcher contract: struct is 40 bytes");
static_assert(offsetof(Constants, version) == 8, "patcher contract");
static_assert(offsetof(Constants, tier2_depth_m) == 12, "patcher contract");
static_assert(offsetof(Constants, flood_threshold) == 16, "patcher contract");
static_assert(offsetof(Constants, motor_damage_per_s) == 20, "patcher contract");
static_assert(offsetof(Constants, tier) == 24, "patcher contract");
static_assert(offsetof(TierConstants, seep_per_s) == 0, "patcher contract");
static_assert(offsetof(TierConstants, hull_mult) == 4, "patcher contract");

// Ticket 12's defaults, leakage halved by ticket 30 (ADR-0012), motor flooding
// damage's as Stoss recommended (threshold 20 %, 10 %/s), after the patcher's conversions:
// leakage p %/min is p/6000 per game-second, the flooding threshold t % is
// t/100, motor damage a % per game-second is a/100.
constexpr Constants kDefaultConstants = {
    {'A', 'P', 'E', 'D', 'P', 'T', 'H', '1'},
    kVersion,
    150.0f,
    0.20f,
    0.10f,
    {
        {2.0f / 6000.0f, 1.25f},
        {7.5f / 6000.0f, 1.5f},
    },
};

// Whether a struct read out of ape.act can be trusted, and if not, why.
struct Verdict {
  bool ok;
  const char* reason;  // static text; empty when ok
};

Verdict verify(const Constants& c);

}  // namespace depth_stress
}  // namespace ape

#endif  // APE_MODS_DEPTH_STRESS_CONSTANTS_HPP
