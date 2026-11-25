#ifndef APE_APE_ACT_DEPTH_STRESS_CONSTANTS_HPP
#define APE_APE_ACT_DEPTH_STRESS_CONSTANTS_HPP

// ape.act's one instance of the depth-stress constants struct, which the
// patcher rewrites in the deployed file (ADR-0006).

#include "mods/depth_stress/constants.hpp"

namespace ape {
namespace depth_stress {

// The struct as it sits in this ape.act's .data, i.e. as the patcher left
// it. Never folded to the build-time defaults.
Constants read_deployed_constants();

}  // namespace depth_stress
}  // namespace ape

#endif  // APE_APE_ACT_DEPTH_STRESS_CONSTANTS_HPP
