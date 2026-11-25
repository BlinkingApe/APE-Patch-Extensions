#ifndef APE_CORE_AOB_HPP
#define APE_CORE_AOB_HPP

// AOB (array-of-bytes) signatures (ADR-0003): a byte pattern with wildcards,
// found in a module's code, from which an absolute address is read.
//
// Pure: it scans a byte range it is given and never touches the process.

#include <cstddef>
#include <cstdint>
#include <cstring>

namespace ape {
namespace aob {

// "8B 3D ?? ?? ?? ?? 85 FF": two hex digits per byte, "??" for a wildcard,
// separated by single spaces. `operand_at` is the byte offset of the
// 4-byte absolute address the signature exists to recover.
struct Signature {
  const char* name;
  const char* pattern;
  std::size_t operand_at;
};

constexpr std::size_t kMaxPattern = 64;

struct Compiled {
  std::uint8_t bytes[kMaxPattern];
  bool must[kMaxPattern];
  std::size_t length = 0;
};

inline int hex_digit(char c) {
  if (c >= '0' && c <= '9') return c - '0';
  if (c >= 'A' && c <= 'F') return c - 'A' + 10;
  if (c >= 'a' && c <= 'f') return c - 'a' + 10;
  return -1;
}

// False on a malformed pattern.
inline bool compile(const char* pattern, Compiled& out) {
  out.length = 0;
  const char* p = pattern;
  while (*p) {
    if (out.length == kMaxPattern) return false;
    if (p[0] == '?' && p[1] == '?') {
      out.bytes[out.length] = 0;
      out.must[out.length] = false;
    } else {
      const int hi = hex_digit(p[0]);
      const int lo = hi < 0 ? -1 : hex_digit(p[1]);
      if (lo < 0) return false;
      out.bytes[out.length] = static_cast<std::uint8_t>(hi * 16 + lo);
      out.must[out.length] = true;
    }
    ++out.length;
    p += 2;
    if (*p == ' ') ++p;
    else if (*p) return false;
  }
  return out.length > 0;
}

// How many times the pattern occurs in [data, data + size), and where the
// first match starts. Counting past the first is what proves uniqueness.
inline std::size_t count(const Compiled& c, const std::uint8_t* data,
                         std::size_t size, std::size_t* first) {
  std::size_t hits = 0;
  if (size < c.length) return 0;
  for (std::size_t i = 0; i + c.length <= size; ++i) {
    std::size_t j = 0;
    while (j < c.length && (!c.must[j] || data[i + j] == c.bytes[j])) ++j;
    if (j == c.length) {
      if (hits == 0 && first) *first = i;
      ++hits;
    }
  }
  return hits;
}

}  // namespace aob
}  // namespace ape

#endif  // APE_CORE_AOB_HPP
