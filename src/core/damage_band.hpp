#ifndef APE_CORE_DAMAGE_BAND_HPP
#define APE_CORE_DAMAGE_BAND_HPP

// The crush-depth damage-band decision function.
//
// This is APE's one automated-test seam: a pure function with no I/O, no
// Windows headers, and no game-memory access. The DllMain hook reads live
// depth/crush-depth from engine state and feeds them here; the test binary
// feeds synthetic values. Both link the same object.
//
// See .scratch/crush-depth-damage/PRD.md and docs/adr/0001-0005.

namespace ape {

// Damage bands are configured as window widths expressed as a fraction of the
// submarine's crush depth, applied cumulatively past crush depth:
//   light  covers [0,               light)
//   medium covers [light,           light+medium)
//   heavy  covers [light+medium,    light+medium+heavy)
// Beyond the outer edge of the heavy band, APE does not intervene and stock
// damage behavior applies. A band configured with zero width is simply never
// selected (the next band starts at the same point).
struct BandConfig {
  double light_window;   // fraction of crush depth
  double medium_window;  // fraction of crush depth
  double heavy_window;   // fraction of crush depth
  double light_rate;     // flat damage-per-tick within the light band
  double medium_rate;    // flat damage-per-tick within the medium band
  double heavy_rate;     // flat damage-per-tick within the heavy band
};

enum class Band { Light, Medium, Heavy, PastHeavy };

struct BandResult {
  Band band;             // which band the depth fell into
  double rate;           // damage-per-tick to apply (0 when deferring)
  bool defer_to_stock;   // true iff past the heavy band: apply stock behavior
};

// depth and crush_depth share units; only their ratio matters. Callers invoke
// this at or below crush depth (fraction under = (depth - crush_depth)/crush_depth,
// clamped at 0 so "exactly at crush depth" lands in the light band).
BandResult select_band(double depth, double crush_depth, const BandConfig& cfg);

}  // namespace ape

#endif  // APE_CORE_DAMAGE_BAND_HPP
