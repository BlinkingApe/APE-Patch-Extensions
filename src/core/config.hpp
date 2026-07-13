#ifndef APE_CORE_CONFIG_HPP
#define APE_CORE_CONFIG_HPP

// Loading of the [CrushDepthDamage] section of data/Cfg/ape.cfg into the
// BandConfig struct the damage-band decision function consumes.
//
// Per the spec's testing decision, real-file config parsing is NOT part of the
// automated unit-test seam (that seam is ape::select_band alone). It is
// verified manually via tools/config_check. The stream-based entry point is
// factored out so that manual harness can exercise parsing without a file.
//
// See docs/adr/0004-config-format-and-location.md and
// .scratch/crush-depth-damage/issues/04-ape-cfg-config-loading.md.

#include <iosfwd>
#include <string>

#include "core/damage_band.hpp"

namespace ape {

// Parse INI content from `in`, reading the [CrushDepthDamage] section into
// `out`. All six keys are required:
//   light_damage_window / medium_damage_window / heavy_damage_window
//   light_damage_rate   / medium_damage_rate   / heavy_damage_rate
// Section and key names are matched case-insensitively; surrounding whitespace
// and ';'/'#' comments (whole-line or trailing) are ignored.
//
// Returns true on success. On failure returns false, leaves `out` untouched,
// and sets `error` to a human-readable reason (missing section, missing key,
// unparseable or negative number).
bool parse_band_config(std::istream& in, BandConfig& out, std::string& error);

// Open `path` and parse it via parse_band_config. Returns false (with `error`
// set) if the file cannot be opened. Intended to be called once at DLL load.
bool load_band_config(const std::string& path, BandConfig& out,
                      std::string& error);

}  // namespace ape

#endif  // APE_CORE_CONFIG_HPP
