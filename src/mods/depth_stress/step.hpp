#ifndef APE_MODS_DEPTH_STRESS_STEP_HPP
#define APE_MODS_DEPTH_STRESS_STEP_HPP

// The depth-stress step: the pure seam (PRD "The depth-stress step").
//
// Placeholder. It takes the constants, the previous step state and either a
// sample or "no sample", and returns the writes. Tickets 24-26 fill in the
// sample, the state, the generator, and the multiplier, seepage and wear;
// for now nothing is ever written.

#include <vector>

#include "mods/depth_stress/constants.hpp"

namespace ape {
namespace depth_stress {

// One poll's reading of game memory (PRD "The sample"). Fields land with
// ticket 24.
struct Sample {};

// What the step carries from one poll to the next. Fields land with ticket 24.
struct StepState {};

// A value the writer puts back into game memory.
struct Write {
  enum class Target { Hull, Flood, PortEngine, StarboardEngine };
  Target target;
  float value;
};

struct StepResult {
  std::vector<Write> writes;
};

// `sample` is null when the reader couldn't produce one this poll.
StepResult step(const Constants& constants, StepState& state,
                const Sample* sample);

}  // namespace depth_stress
}  // namespace ape

#endif  // APE_MODS_DEPTH_STRESS_STEP_HPP
