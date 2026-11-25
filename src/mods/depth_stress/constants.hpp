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
constexpr std::uint32_t kVersion = 1;

// Hard ceilings, fixed here and never part of the struct (PRD user story 47):
// seepage alone never drowns the room's crew, and wear never leaves an engine
// the crew can't repair.
constexpr float kSeepCeiling = 0.8f;
// While seeping, the level never rests below this. The crew pumps a compartment
// to exactly 0, and stock re-arms its "We have flooding, sir!" report only at
// exactly 0 (ticket 28); a floor above one pump step (~0.0024 to 0.005 per
// game-second) keeps the report to one per dive. Not part of the struct.
constexpr float kSeepFloor = 0.01f;
constexpr float kWearCeiling = 0.9f;

// Tier 1 starts at this depth whatever the class; it is not a field.
constexpr float kTier1DepthM = 100.0f;

struct TierConstants {
  float seep_per_s;         // flood fraction added per game-second (0 = off)
  float wear_amount;        // engine damage added per hit (0 = off)
  float wear_chance_per_s;  // chance of a hit per game-second
  float hull_mult;          // hull damage multiplier (1.0 = off)
};

struct Constants {
  char magic[8];
  std::uint32_t version;
  float tier2_depth_m;
  TierConstants tier[2];  // [0] tier 1, [1] tier 2 (and tier 3)
};

// The layout gen_manifest.py records for the patcher (DEPTH_STRESS_FIELDS).
// A field moved here without moving it there fails the build.
static_assert(sizeof(TierConstants) == 16, "patcher contract: tier is 16 bytes");
static_assert(sizeof(Constants) == 48, "patcher contract: struct is 48 bytes");
static_assert(offsetof(Constants, version) == 8, "patcher contract");
static_assert(offsetof(Constants, tier2_depth_m) == 12, "patcher contract");
static_assert(offsetof(Constants, tier) == 16, "patcher contract");
static_assert(offsetof(TierConstants, seep_per_s) == 0, "patcher contract");
static_assert(offsetof(TierConstants, wear_amount) == 4, "patcher contract");
static_assert(offsetof(TierConstants, wear_chance_per_s) == 8, "patcher contract");
static_assert(offsetof(TierConstants, hull_mult) == 12, "patcher contract");

// Ticket 12's defaults, after the patcher's conversions: leakage p %/min is
// p/6000 per game-second, wear rate m min is 1/(60*m) per game-second, wear
// amount a % is a/100.
constexpr Constants kDefaultConstants = {
    {'A', 'P', 'E', 'D', 'P', 'T', 'H', '1'},
    kVersion,
    150.0f,
    {
        {4.0f / 6000.0f, 0.075f, 1.0f / (60.0f * 200.0f), 1.25f},
        {15.0f / 6000.0f, 0.225f, 1.0f / (60.0f * 130.0f), 1.5f},
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
