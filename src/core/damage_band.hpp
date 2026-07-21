#ifndef APE_CORE_DAMAGE_BAND_HPP
#define APE_CORE_DAMAGE_BAND_HPP

// The crush-depth damage-band decision function.
//
// This is APE's one automated-test seam: a pure function with no I/O, no
// Windows headers, and no game-memory access. APE's own loop reads live
// depth/crush-depth from engine state and feeds them here; the test binary
// feeds synthetic values. Both link the same object.
//
// APE owns 100% of crush-depth damage (stock's own bleed is neutralized), so
// there is no "hand back to stock" zone: the heavy band is open-ended and
// applies until the hull is gone.
//
// See .scratch/crush-depth-damage/PRD.md and docs/adr/0001-0005.

namespace ape {

// Damage bands are configured as two cumulative window boundaries expressed as
// a fraction of the submarine's crush depth, applied past crush depth:
//   light  covers [0,               light_window)
//   medium covers [light_window,    light_window + medium_window)
//   heavy  covers [light_window + medium_window, +inf)   -- open-ended
// A band configured with zero width is simply never selected (the next band
// starts at the same point).
struct BandConfig {
  double light_window;   // fraction of crush depth
  double medium_window;  // fraction of crush depth
  double light_rate;     // flat HP-per-game-second within the light band
  double medium_rate;    // flat HP-per-game-second within the medium band
  double heavy_rate;     // flat HP-per-game-second within the heavy band
};

enum class Band { Light, Medium, Heavy };

struct BandResult {
  Band band;   // which band the depth fell into
  double rate; // HP-per-game-second to apply; the caller multiplies by dt
};

// depth and crush_depth share units; only their ratio matters. Callers invoke
// this at or below crush depth (fraction under = (depth - crush_depth)/crush_depth,
// clamped at 0 so "exactly at crush depth" lands in the light band). The
// returned rate is HP-per-game-second: the caller multiplies it by the
// game-time delta dt (Δ mission clock) and subtracts from HP.
BandResult select_band(double depth, double crush_depth, const BandConfig& cfg);

}  // namespace ape

#endif  // APE_CORE_DAMAGE_BAND_HPP
