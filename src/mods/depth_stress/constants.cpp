#include "mods/depth_stress/constants.hpp"

#include <cstring>

namespace ape {
namespace depth_stress {

Verdict verify(const Constants& c) {
  if (std::memcmp(c.magic, kMagic, sizeof(kMagic)) != 0)
    return {false, "marker mismatch"};
  if (c.version != kVersion) return {false, "version mismatch"};
  return {true, ""};
}

}  // namespace depth_stress
}  // namespace ape
