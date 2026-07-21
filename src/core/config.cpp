#include "core/config.hpp"

#include <cctype>
#include <cstdlib>
#include <fstream>
#include <istream>
#include <string>

namespace ape {
namespace {

// Trim ASCII whitespace from both ends.
std::string trim(const std::string& s) {
  std::size_t begin = 0;
  std::size_t end = s.size();
  while (begin < end &&
         std::isspace(static_cast<unsigned char>(s[begin]))) {
    ++begin;
  }
  while (end > begin &&
         std::isspace(static_cast<unsigned char>(s[end - 1]))) {
    --end;
  }
  return s.substr(begin, end - begin);
}

std::string to_lower(std::string s) {
  for (char& c : s) {
    c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
  }
  return s;
}

// Strip a trailing ';' or '#' comment. Values are numeric, so neither
// character can appear legitimately mid-line.
std::string strip_comment(const std::string& line) {
  const std::size_t pos = line.find_first_of(";#");
  if (pos == std::string::npos) return line;
  return line.substr(0, pos);
}

// Parse a whole numeric token into `value`. Requires the entire (trimmed,
// non-empty) token to be consumed and the result to be non-negative.
bool parse_number(const std::string& token, double& value) {
  if (token.empty()) return false;
  const char* begin = token.c_str();
  char* parse_end = nullptr;
  const double parsed = std::strtod(begin, &parse_end);
  if (parse_end != begin + token.size()) return false;  // trailing garbage
  if (parsed < 0.0) return false;
  value = parsed;
  return true;
}

// One target key: its config name and the BandConfig field it fills.
struct KeyBinding {
  const char* name;               // lowercased config key
  double BandConfig::* field;     // destination field
};

}  // namespace

bool parse_band_config(std::istream& in, BandConfig& out, std::string& error) {
  const KeyBinding bindings[] = {
      {"light_damage_window", &BandConfig::light_window},
      {"medium_damage_window", &BandConfig::medium_window},
      {"light_damage_rate", &BandConfig::light_rate},
      {"medium_damage_rate", &BandConfig::medium_rate},
      {"heavy_damage_rate", &BandConfig::heavy_rate},
  };
  constexpr int kKeyCount = sizeof(bindings) / sizeof(bindings[0]);

  BandConfig parsed{};
  bool found[kKeyCount] = {false};
  bool in_section = false;
  bool saw_section = false;

  std::string raw;
  while (std::getline(in, raw)) {
    const std::string line = trim(strip_comment(raw));
    if (line.empty()) continue;

    // Section header: [Name]
    if (line.front() == '[') {
      if (line.back() != ']') {
        error = "malformed section header: " + line;
        return false;
      }
      const std::string name = to_lower(trim(line.substr(1, line.size() - 2)));
      in_section = (name == "crushdepthdamage");
      if (in_section) saw_section = true;
      continue;
    }

    if (!in_section) continue;  // keys outside our section are not ours

    const std::size_t eq = line.find('=');
    if (eq == std::string::npos) {
      error = "expected key=value in [CrushDepthDamage]: " + line;
      return false;
    }
    const std::string key = to_lower(trim(line.substr(0, eq)));
    const std::string value = trim(line.substr(eq + 1));

    // A key matching none of the bindings is ignored (forward-compatible).
    for (int i = 0; i < kKeyCount; ++i) {
      if (key == bindings[i].name) {
        double number = 0.0;
        if (!parse_number(value, number)) {
          error = "invalid value for " + key + ": '" + value + "'";
          return false;
        }
        parsed.*(bindings[i].field) = number;  // last write wins on duplicates
        found[i] = true;
        break;
      }
    }
  }

  if (!saw_section) {
    error = "missing [CrushDepthDamage] section";
    return false;
  }
  for (int i = 0; i < kKeyCount; ++i) {
    if (!found[i]) {
      error = std::string("missing key: ") + bindings[i].name;
      return false;
    }
  }

  out = parsed;
  return true;
}

bool load_band_config(const std::string& path, BandConfig& out,
                      std::string& error) {
  std::ifstream file(path);
  if (!file) {
    error = "cannot open config file: " + path;
    return false;
  }
  return parse_band_config(file, out, error);
}

}  // namespace ape
