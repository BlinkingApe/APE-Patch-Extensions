#include "mods/crush_depth_damage/damage_band.hpp"

namespace ape {

BandResult select_band(double depth, double crush_depth, const BandConfig& cfg) {
  // Fraction under crush depth. Clamp at 0 so that at-or-above crush depth
  // lands in the light band rather than producing a negative fraction.
  double fraction = 0.0;
  if (crush_depth > 0.0) {
    fraction = (depth - crush_depth) / crush_depth;
    if (fraction < 0.0) fraction = 0.0;
  }

  // Cumulative upper edges of the two bounded bands. Heavy is open-ended.
  const double light_edge = cfg.light_window;
  const double medium_edge = light_edge + cfg.medium_window;

  // Each bounded band owns [lower_edge, upper_edge); a zero-width band
  // collapses to an empty interval and is skipped because the next comparison
  // starts equal. Anything past the medium edge is the open-ended heavy band.
  if (fraction < light_edge) {
    return {Band::Light, cfg.light_rate};
  }
  if (fraction < medium_edge) {
    return {Band::Medium, cfg.medium_rate};
  }
  return {Band::Heavy, cfg.heavy_rate};
}

}  // namespace ape
