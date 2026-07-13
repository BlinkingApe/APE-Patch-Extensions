#include "core/damage_band.hpp"

namespace ape {

BandResult select_band(double depth, double crush_depth, const BandConfig& cfg) {
  // Fraction under crush depth. Clamp at 0 so that at-or-above crush depth
  // lands in the light band rather than producing a negative fraction.
  double fraction = 0.0;
  if (crush_depth > 0.0) {
    fraction = (depth - crush_depth) / crush_depth;
    if (fraction < 0.0) fraction = 0.0;
  }

  // Cumulative upper edges of each band.
  const double light_edge = cfg.light_window;
  const double medium_edge = light_edge + cfg.medium_window;
  const double heavy_edge = medium_edge + cfg.heavy_window;

  // Each band owns [lower_edge, upper_edge); a zero-width band collapses to an
  // empty interval and is skipped because the next comparison starts equal.
  if (fraction < light_edge) {
    return {Band::Light, cfg.light_rate, false};
  }
  if (fraction < medium_edge) {
    return {Band::Medium, cfg.medium_rate, false};
  }
  if (fraction < heavy_edge) {
    return {Band::Heavy, cfg.heavy_rate, false};
  }
  return {Band::PastHeavy, 0.0, true};
}

}  // namespace ape
