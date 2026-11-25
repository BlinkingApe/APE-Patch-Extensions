#ifndef APE_MODS_DEPTH_STRESS_STEP_HPP
#define APE_MODS_DEPTH_STRESS_STEP_HPP

// The depth-stress step: the pure seam (PRD "The depth-stress step").
//
// It takes the constants, the previous step state and either a sample or "no
// sample", and returns the writes. Ticket 24 gives it the hull multiplier;
// seepage (25) and electric-engine wear (26) add their fields to the sample
// and their writes to the result.

#include <cstdint>
#include <random>
#include <vector>

#include "mods/depth_stress/constants.hpp"

namespace ape {
namespace depth_stress {

// One poll's reading of game memory (PRD "The sample"), before the gates.
struct Sample {
  std::uint32_t boat;   // the boat object's address: its identity
  std::int32_t clock_s; // mission clock, whole game-seconds
  float depth_m;        // gauge depth D
  float hull;           // boat +0xB8
  float max_hull;       // boat +0xB4
  float crush_field;    // CD, boat +0xC0: .zon CrashDepth / 10
  float crush_speed;    // CS, boat +0xC4
  float engine_port;    // port electric engine damage, 0 healthy -> 1 destroyed
  float engine_stbd;    // starboard electric engine damage
  // The seep compartment's flood, 0 dry -> 1 full. `has_seep` is false when
  // the reader found no zone for it (then there is no seepage, and the
  // sample is otherwise good).
  float seep_flood;
  bool has_seep;
};

// The step's source of randomness: uniform doubles in [0, 1). Taken as an
// input so tests can force hits and seed runs (PRD "The depth-stress step").
class Random {
 public:
  virtual ~Random() = default;
  virtual double uniform() = 0;
};

// The shipped generator: a 32-bit Mersenne twister, never MSVC rand()
// (ticket 12's trap), seeded once by the caller.
class Mt19937Random : public Random {
 public:
  explicit Mt19937Random(std::uint32_t seed) : gen_(seed) {}
  double uniform() override { return dist_(gen_); }

 private:
  std::mt19937 gen_;
  std::uniform_real_distribution<double> dist_{0.0, 1.0};
};

// Null when every plausibility gate holds (PRD "Plausibility gates"), else
// why not. A sample that fails one is treated as no sample.
const char* implausible(const Sample& s);

// What the step carries from one poll to the next. Opaque to callers: start
// from a value-initialised one and hand it back each poll.
struct StepState {
  bool has_baseline = false;
  std::uint32_t boat = 0;
  float hull = 0.0f;           // after APE's own write
  std::int32_t clock_s = 0;
  bool clock_was_zero = false; // save-load guard 2
};

// A value the writer puts back into game memory.
struct Write {
  enum class Target { Hull, Flood, PortEngine, StarboardEngine };
  Target target;
  float old_value;
  float value;
};

enum class Tier { None = 0, One = 1, Two = 2, Three = 3 };

// What the step saw, for the log (PRD "Logging"). Only meaningful when the
// poll had a plausible sample and wasn't a baseline.
struct Diagnostics {
  Tier tier = Tier::None;
  float onset_m = 0.0f;    // stock's crush onset, 7*E metres
  std::int32_t dt_s = 0;   // whole game-seconds since the last poll
  float drop = 0.0f;       // N: previous hull - hull
  float exempt = 0.0f;     // X: the crush-ramp share of N
};

struct StepResult {
  std::vector<Write> writes;
  Diagnostics diag;
  // Null when the poll ran; else why it was skipped or only re-baselined.
  const char* skipped = nullptr;
  // True when this poll only took a new baseline (first sample, new boat).
  bool baselined = false;
};

// `sample` is null when the reader couldn't produce one this poll. `rng` feeds
// electric-engine wear; with none, there is no wear.
StepResult step(const Constants& constants, StepState& state,
                const Sample* sample, Random* rng = nullptr);

}  // namespace depth_stress
}  // namespace ape

#endif  // APE_MODS_DEPTH_STRESS_STEP_HPP
