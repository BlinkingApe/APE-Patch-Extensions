// The depth-stress constants struct itself (ADR-0006).
//
// Initialised with the shipped defaults (kDefaultConstants), so the built ape.act is the
// patcher's template: the patcher copies it, finds this struct at the offset
// the manifest records, and overwrites the fields.
//
// It must stay a non-const object in .data. A const one could land in .rdata
// and, worse, be constant-folded into the code that reads it, so the
// patcher's bytes would never be seen. read_deployed_constants() reads it
// through a volatile pointer for the same reason.

#include "ape_act/depth_stress_constants.hpp"

namespace ape {
namespace depth_stress {

Constants g_constants = kDefaultConstants;

Constants read_deployed_constants() {
  const volatile unsigned char* src =
      reinterpret_cast<const volatile unsigned char*>(&g_constants);
  Constants c{};
  unsigned char* dst = reinterpret_cast<unsigned char*>(&c);
  for (std::size_t i = 0; i < sizeof(Constants); ++i) dst[i] = src[i];
  return c;
}

}  // namespace depth_stress
}  // namespace ape
