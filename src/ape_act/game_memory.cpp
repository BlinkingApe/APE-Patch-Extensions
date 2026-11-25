// The game-memory reader and writer. See game_memory.hpp, and
// docs/re-notes/09-depth-stress-runtime-anchors.md for every signature and
// chain and the OAE build they were proven on.

#include "ape_act/game_memory.hpp"

#include <windows.h>

#include <cstddef>
#include <cstring>

#include "core/aob.hpp"

namespace ape {
namespace game {

namespace {

// Fields of the boat object (PRD "The sample").
constexpr std::uint32_t kHull = 0xB8;
constexpr std::uint32_t kMaxHull = 0xB4;
constexpr std::uint32_t kRatedCrushField = 0xC0;
constexpr std::uint32_t kCrushSpeed = 0xC4;

// The boat root: stock's own store of the player-boat root pointer,
// `mov [0x554A88],ecx; push 0x58; mov [esi+0x10],ebx; call [iat]` @0x004BA972.
constexpr aob::Signature kBoatRoot = {
    "boat root", "89 0D ?? ?? ?? ?? 6A 58 89 5E 10 FF 15", 2};

// The depth root: a stock walk of the list it heads,
// `mov edi,[0x6307BC]; test edi,edi; je; lea ebx,[ebx]; mov eax,[edi]`
// @0x0050EF70.
constexpr aob::Signature kDepthRoot = {
    "depth root", "8B 3D ?? ?? ?? ?? 85 FF 74 15 8D 9B 00 00 00 00 8B 07", 2};

// The mission clock: no stock instruction names h.sie's mirror, so this is
// h.sie's cave at 0x0051CFC7, `mov [0x63FF80],eax; mov [esi+0x150],eax; jmp`,
// which stores stock's clock and the mirror from one register.
constexpr aob::Signature kClock = {
    "clock", "A3 ?? ?? ?? ?? 89 86 50 01 00 00 E9", 1};

// The electric engines: the engine singleton's root, and the offset of the
// electric block's pair of damage-float pointers (docs/re-notes/08 s1; both
// unique on OAE). The chain is [[[[root]+0x4]+off]+0] / [...+0x4].
constexpr aob::Signature kEngineRoot = {
    "engine root", "89 35 ?? ?? ?? ?? 57 8D 7E 10", 2};
constexpr aob::Signature kEngineBlockOff = {
    "engine block offset", "8B 80 ?? ?? ?? ?? 8D 77 FC", 2};

// The zone-state list (docs/re-notes/10): head variable, and the table that
// maps a zone index to a compartment id (a signed byte, -1 for none). Both are
// operands of the crew-report routine's loop at 0x0043601D...0x00436049.
constexpr aob::Signature kZoneHead = {
    "zone list head", "8B 3D ?? ?? ?? ?? 85 FF C7 44 24 40 00 00 00 00", 2};
constexpr aob::Signature kZoneTable = {
    "zone compartment table", "8A 8D ?? ?? ?? ?? 80 F9 FF 89 6C 24 2C", 2};

// The ordered machine telegraph: h.sie's diesel cave (docs/re-notes/04 s2)
// stores it once a second, `mov eax,[eax+0x320]; mov eax,[eax];
// mov [0x63E1D4],eax` @0x00635065, whether or not Diesel Damage is enabled.
constexpr aob::Signature kTelegraph = {
    "telegraph", "8B 80 20 03 00 00 8B 00 89 05 ?? ?? ?? ??", 10};

bool read_u32(std::uint32_t addr, std::uint32_t& out) {
  if (addr < 0x10000) return false;  // null, or a null plus an offset
  SIZE_T n = 0;
  return ReadProcessMemory(GetCurrentProcess(),
                           reinterpret_cast<LPCVOID>(static_cast<std::uintptr_t>(addr)),
                           &out, sizeof out, &n) &&
         n == sizeof out;
}

bool read_f32(std::uint32_t addr, float& out) {
  std::uint32_t bits;
  if (!read_u32(addr, bits)) return false;
  std::memcpy(&out, &bits, sizeof out);
  return true;
}

// Follows [[root]+o0]+o1... : each step reads a pointer, then adds the next
// offset. Returns false on a null link or a failed read.
bool chain(std::uint32_t root, const std::uint32_t* offsets, std::size_t n,
           std::uint32_t& out) {
  std::uint32_t p;
  if (!read_u32(root, p)) return false;
  for (std::size_t i = 0; i < n; ++i)
    if (!read_u32(p + offsets[i], p)) return false;
  out = p;
  return true;
}

// Finds `sig` in every executable section of the host image; it must match
// exactly once across all of them (OAE's sh3.exe has three).
const char* resolve(const aob::Signature& sig, std::uint32_t& out) {
  aob::Compiled c;
  if (!aob::compile(sig.pattern, c)) return "malformed pattern";
  const auto* base = reinterpret_cast<const std::uint8_t*>(GetModuleHandleW(nullptr));
  if (!base) return "no host image";
  const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
  const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS32*>(base + dos->e_lfanew);
  const IMAGE_SECTION_HEADER* sec = IMAGE_FIRST_SECTION(nt);
  std::size_t hits = 0;
  const std::uint8_t* match = nullptr;
  for (WORD i = 0; i < nt->FileHeader.NumberOfSections; ++i, ++sec) {
    if (!(sec->Characteristics & IMAGE_SCN_MEM_EXECUTE)) continue;
    const std::uint8_t* start = base + sec->VirtualAddress;
    std::size_t size = sec->Misc.VirtualSize;
    if (sec->SizeOfRawData < size) size = sec->SizeOfRawData;
    std::size_t first = 0;
    const std::size_t n = aob::count(c, start, size, &first);
    if (n && !match) match = start + first;
    hits += n;
  }
  if (hits == 0) return "no match";
  if (hits > 1) return "more than one match";
  std::memcpy(&out, match + sig.operand_at, sizeof out);
  return nullptr;
}

}  // namespace

Anchors resolve_anchors() {
  Anchors a;
  const aob::Signature* sigs[] = {&kBoatRoot,  &kDepthRoot,      &kClock,
                                  &kEngineRoot, &kEngineBlockOff, &kZoneHead,
                                  &kZoneTable};
  std::uint32_t* outs[] = {&a.boat_root,   &a.depth_root,       &a.clock,
                           &a.engine_root, &a.engine_block_off, &a.zone_head,
                           &a.zone_table};
  static_assert(sizeof(sigs) / sizeof(sigs[0]) == sizeof(outs) / sizeof(outs[0]),
                "one root per signature");
  for (std::size_t i = 0; i < sizeof(sigs) / sizeof(sigs[0]); ++i) {
    if (const char* why = resolve(*sigs[i], *outs[i])) {
      a.failed = sigs[i]->name;
      a.why = why;
      return a;
    }
  }
  // The telegraph is optional, like its value: without it there is no motor
  // damage, and the other effects run (ticket 31).
  if (const char* why = resolve(kTelegraph, a.telegraph)) {
    a.telegraph = 0;
    a.telegraph_why = why;
  }
  return a;
}

namespace {

const char* g_seep_why = nullptr;
const char* g_telegraph_why = nullptr;

bool read_u8(std::uint32_t addr, std::uint8_t& out) {
  if (addr < 0x10000) return false;
  SIZE_T n = 0;
  return ReadProcessMemory(GetCurrentProcess(),
                           reinterpret_cast<LPCVOID>(static_cast<std::uintptr_t>(addr)),
                           &out, 1, &n) &&
         n == 1;
}

// Walks the zone list and picks the seep zone from the live table: the first
// zone in list order that maps to compartment 4, the electric engine room, on
// every class (ADR-0012, ticket 30). Never by class. Sets `flood_addr`, or
// returns why not.
const char* find_seep_flood(const Anchors& a, std::uint32_t& flood_addr) {
  flood_addr = 0;
  std::uint32_t node;
  if (!read_u32(a.zone_head, node)) return "zone list head unreadable";
  // A bounded walk: a real list is under a hundred zones. The compartment ids
  // are collected first so the pick itself is the pure, tested
  // depth_stress::pick_seep_zone; table bytes are signed (0xFF = none = -1).
  constexpr int kMaxZones = 512;
  std::int8_t comps[kMaxZones];
  std::uint32_t states[kMaxZones];
  int n = 0;
  for (; n < kMaxZones && node; ++n) {
    std::uint32_t state, next, desc, zone;
    if (!read_u32(node + 0x14, state) || !read_u32(state + 0x10, desc) ||
        !read_u32(desc + 0x8, zone) || !read_u32(node + 0x4, next))
      return "zone list unreadable";
    // The crew-report loop's own bound: zones from 200 up are never looked up.
    std::uint8_t comp;
    comps[n] = (zone < 200 && read_u8(a.zone_table + zone, comp))
                   ? static_cast<std::int8_t>(comp)
                   : std::int8_t{-1};
    states[n] = state;
    node = next;
  }
  const int i = depth_stress::pick_seep_zone(comps, n);
  if (i < 0) return "no zone maps to compartment 4 (electric engine room)";
  flood_addr = states[i] + 0x1C;
  return nullptr;
}

}  // namespace

const char* seep_unavailable() { return g_seep_why; }

const char* telegraph_unavailable() { return g_telegraph_why; }

bool read_sample(const Anchors& a, depth_stress::Sample& out, Targets& targets,
                 const char** why) {
  if (a.failed) {
    *why = "an anchor did not resolve";
    return false;
  }

  // Gauge depth: [[[depth root]+0x4]+0x4]+0x54.
  static const std::uint32_t kDepthLinks[] = {0x4, 0x4};
  std::uint32_t depth_obj;
  if (!chain(a.depth_root, kDepthLinks, 2, depth_obj) ||
      !read_f32(depth_obj + 0x54, out.depth_m)) {
    *why = "depth chain unresolved";
    return false;
  }

  std::uint32_t clock;
  if (!read_u32(a.clock, clock)) {
    *why = "clock unreadable";
    return false;
  }
  out.clock_s = static_cast<std::int32_t>(clock);

  // The boat: [[[[[boat root]+0xC4]+0x2240]+0x24]+0x4C]; the pointer itself
  // is the boat's identity.
  static const std::uint32_t kBoatLinks[] = {0xC4, 0x2240, 0x24, 0x4C};
  std::uint32_t boat;
  if (!chain(a.boat_root, kBoatLinks, 4, boat) || boat < 0x10000) {
    *why = "boat chain unresolved";
    return false;
  }
  out.boat = boat;
  targets.boat = boat;
  if (!read_f32(boat + kHull, out.hull) || !read_f32(boat + kMaxHull, out.max_hull) ||
      !read_f32(boat + kRatedCrushField, out.crush_field) ||
      !read_f32(boat + kCrushSpeed, out.crush_speed)) {
    *why = "boat fields unreadable";
    return false;
  }

  // The electric engines: a pair of pointers, each to the engine's damage
  // float ([port, starboard]). The offset is bounded so a misread anchor
  // cannot send the chain far afield.
  std::uint32_t block;
  const std::uint32_t engine_links[] = {0x4, a.engine_block_off};
  if (a.engine_block_off == 0 || a.engine_block_off > 0x1000 ||
      !chain(a.engine_root, engine_links, 2, block) ||
      !read_u32(block + 0, targets.engine_port) ||
      !read_u32(block + 4, targets.engine_stbd) ||
      !read_f32(targets.engine_port, out.engine_port) ||
      !read_f32(targets.engine_stbd, out.engine_stbd)) {
    *why = "engine chain unresolved";
    return false;
  }

  // The seep compartment is optional: without one there is no seepage, and
  // the rest of the sample stands.
  out.has_seep = false;
  g_seep_why = find_seep_flood(a, targets.seep_flood);
  if (!g_seep_why) {
    if (read_f32(targets.seep_flood, out.seep_flood))
      out.has_seep = true;
    else
      g_seep_why = "seep flood unreadable";
  }

  // The telegraph is optional too: without a plausible value there is no
  // motor damage this poll, and the rest of the sample stands.
  out.has_telegraph = false;
  if (!a.telegraph)
    g_telegraph_why = "telegraph anchor unresolved";
  else if (!read_f32(a.telegraph, out.telegraph))
    g_telegraph_why = "telegraph unreadable";
  else if (!depth_stress::telegraph_plausible(out.telegraph))
    g_telegraph_why = "telegraph outside [-1, 1]";
  else {
    g_telegraph_why = nullptr;
    out.has_telegraph = true;
  }
  return true;
}

bool write_hull(std::uint32_t boat, float value) {
  SIZE_T n = 0;
  return WriteProcessMemory(GetCurrentProcess(),
                            reinterpret_cast<LPVOID>(static_cast<std::uintptr_t>(boat + kHull)),
                            &value, sizeof value, &n) &&
         n == sizeof value;
}

bool write_engine(std::uint32_t address, float value) {
  if (address < 0x10000) return false;
  SIZE_T n = 0;
  return WriteProcessMemory(GetCurrentProcess(),
                            reinterpret_cast<LPVOID>(static_cast<std::uintptr_t>(address)),
                            &value, sizeof value, &n) &&
         n == sizeof value;
}

bool write_flood(std::uint32_t address, float value) {
  return write_engine(address, value);  // the same one-float store
}

}  // namespace game
}  // namespace ape
