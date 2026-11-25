// ape.act entry point: the loader (PRD Modules).
//
// DllMain copies the depth-stress constants struct out of ape.act's own .data
// and checks its marker and version (ADR-0006), a plain memory compare. It
// then starts one background thread and returns. Nothing else happens under
// the loader lock: no file I/O, no scanning, no game memory, and no function
// is hooked (ADR-0001).
//
// The thread does the logging. On a matching marker it logs the constants,
// resolves the game-memory anchors and runs the poll loop at about 10 Hz:
// read a sample, step, write. On a mismatch it logs the reason and returns
// without ever entering the loop (fail inert).

#include <windows.h>

#include <atomic>
#include <random>
#include <string>

#include "ape_act/depth_stress_constants.hpp"
#include "ape_act/game_memory.hpp"
#include "core/log.hpp"
#include "mods/depth_stress/constants.hpp"
#include "mods/depth_stress/step.hpp"

namespace ds = ape::depth_stress;

namespace {

constexpr DWORD kPollMs = 100;  // ~10 Hz; arithmetic runs on game time, not this

// A skip whose reason hasn't changed is logged again at most this often, and
// no skip at all more often than kSkipGapMs (reasons can alternate, e.g. while
// a mission loads).
constexpr DWORD kSkipRepeatMs = 30000;
constexpr DWORD kSkipGapMs = 1000;

std::atomic<bool> g_stop{false};

// Set once in DllMain, before the thread exists, and only read after.
ds::Constants g_loaded{};
ds::Verdict g_verdict{false, "not loaded"};

// Directory of the host process image (the game root), where the log goes.
std::string game_root() {
  char buf[MAX_PATH] = {0};
  const DWORD n = GetModuleFileNameA(nullptr, buf, MAX_PATH);
  std::string path(buf, n);
  const std::size_t slash = path.find_last_of("\\/");
  return slash == std::string::npos ? std::string() : path.substr(0, slash);
}

void log_constants(ape::Logger& log, const ds::Constants& c) {
  log.logf("[depth-stress] marker ok: %.8s v%u", c.magic,
           static_cast<unsigned>(c.version));
  log.logf("[depth-stress] tier-2 depth %.1f m (tier 1 from %.1f m)",
           c.tier2_depth_m, ds::kTier1DepthM);
  for (int t = 0; t < 2; ++t) {
    const ds::TierConstants& k = c.tier[t];
    log.logf("[depth-stress] tier %d: seep %.6g /s, wear %.6g per hit at "
             "%.6g /s, hull x%.4g",
             t + 1, k.seep_per_s, k.wear_amount, k.wear_chance_per_s,
             k.hull_mult);
  }
  log.logf("[depth-stress] ceilings (fixed): seep %.2f, wear %.2f",
           ds::kSeepCeiling, ds::kWearCeiling);
}

// Logs a skipped poll's reason: at once when it differs from the last one
// logged, otherwise at most every kSkipRepeatMs with a count of the polls in
// between. Reasons are static strings, so a pointer compare is enough.
class SkipLog {
 public:
  explicit SkipLog(ape::Logger& log) : log_(log) {}

  void skipped(const char* why) {
    const DWORD now = GetTickCount();
    const DWORD since = now - logged_at_;
    if (since < kSkipGapMs || (why == last_ && since < kSkipRepeatMs)) {
      ++repeats_;
      return;
    }
    if (why == last_)
      log_.logf("[depth-stress] skip: %s (x%lu since last logged)", why,
                static_cast<unsigned long>(repeats_ + 1));
    else
      log_.logf("[depth-stress] skip: %s", why);
    last_ = why;
    logged_at_ = now;
    repeats_ = 0;
  }

  // A poll that ran: the next skip is news whatever its reason.
  void ran() { last_ = nullptr; }

 private:
  ape::Logger& log_;
  const char* last_ = nullptr;
  DWORD logged_at_ = 0;
  unsigned long repeats_ = 0;
};

void log_writes(ape::Logger& log, const ds::Sample& s, const ds::StepResult& r,
                bool ok) {
  for (const ds::Write& w : r.writes) {
    if (w.target == ds::Write::Target::Flood) {
      log.logf("[depth-stress] seep%s: D %.2f m, tier %d, dt %ld s, flood "
               "%.4f -> %.4f",
               ok ? "" : " FAILED", s.depth_m, static_cast<int>(r.diag.tier),
               static_cast<long>(r.diag.dt_s), w.old_value, w.value);
      continue;
    }
    if (w.target == ds::Write::Target::PortEngine ||
        w.target == ds::Write::Target::StarboardEngine) {
      log.logf("[depth-stress] wear%s: D %.2f m, tier %d, dt %ld s, %s engine "
               "%.4f -> %.4f",
               ok ? "" : " FAILED", s.depth_m, static_cast<int>(r.diag.tier),
               static_cast<long>(r.diag.dt_s),
               w.target == ds::Write::Target::PortEngine ? "port" : "starboard",
               w.old_value, w.value);
      continue;
    }
    log.logf("[depth-stress] write%s: D %.2f m, tier %d, onset %.2f m, dt %ld s, "
             "N %.4f, X %.4f, hull %.4f -> %.4f (max %.1f)",
             ok ? "" : " FAILED", s.depth_m, static_cast<int>(r.diag.tier),
             r.diag.onset_m, static_cast<long>(r.diag.dt_s), r.diag.drop,
             r.diag.exempt, w.old_value, w.value, s.max_hull);
  }
}

DWORD WINAPI depth_stress_thread(LPVOID module) {
  // Hold ape.act in memory for the life of the process, so the code this
  // thread runs can never be unmapped under it (DllMain doesn't wait for it).
  HMODULE pinned = nullptr;
  GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                         GET_MODULE_HANDLE_EX_FLAG_PIN,
                     reinterpret_cast<LPCWSTR>(module), &pinned);

  ape::Logger log(game_root() + "\\ape_act.log");
  if (!g_verdict.ok) {
    log.logf("[depth-stress] constants rejected (%s): loop not started",
             g_verdict.reason);
    return 0;
  }
  log_constants(log, g_loaded);
  log.logf("[depth-stress] loop started, polling every %lu ms",
           static_cast<unsigned long>(kPollMs));

  // Scanned once, off the loader lock. An anchor that doesn't resolve leaves
  // every poll skipped with that reason: nothing is ever guessed.
  const ape::game::Anchors anchors = ape::game::resolve_anchors();
  if (anchors.failed)
    log.logf("[depth-stress] anchor '%s' unresolved (%s): every poll will skip",
             anchors.failed, anchors.why);
  else
    log.logf("[depth-stress] anchors: boat root 0x%08lX, depth root 0x%08lX, "
             "clock 0x%08lX, engine root 0x%08lX (+0x%lX), zone head 0x%08lX, "
             "zone table 0x%08lX",
             static_cast<unsigned long>(anchors.boat_root),
             static_cast<unsigned long>(anchors.depth_root),
             static_cast<unsigned long>(anchors.clock),
             static_cast<unsigned long>(anchors.engine_root),
             static_cast<unsigned long>(anchors.engine_block_off),
             static_cast<unsigned long>(anchors.zone_head),
             static_cast<unsigned long>(anchors.zone_table));

  SkipLog skips(log);
  // Seeded once, from the OS entropy source, for the life of the loop.
  std::random_device entropy;
  ds::Mt19937Random rng(entropy());
  ds::StepState state{};
  std::uint32_t last_boat = 0;
  const char* last_seep_why = nullptr;
  while (!g_stop.load()) {
    ds::Sample sample{};
    ape::game::Targets targets;
    const char* why = nullptr;
    const bool have = ape::game::read_sample(anchors, sample, targets, &why);
    const ds::StepResult r =
        ds::step(g_loaded, state, have ? &sample : nullptr, &rng);

    bool ok = true;
    for (const ds::Write& w : r.writes) {
      switch (w.target) {
        case ds::Write::Target::Hull:
          ok = ape::game::write_hull(targets.boat, w.value) && ok;
          break;
        case ds::Write::Target::PortEngine:
          ok = ape::game::write_engine(targets.engine_port, w.value) && ok;
          break;
        case ds::Write::Target::StarboardEngine:
          ok = ape::game::write_engine(targets.engine_stbd, w.value) && ok;
          break;
        case ds::Write::Target::Flood:
          ok = ape::game::write_flood(targets.seep_flood, w.value) && ok;
          break;
      }
    }
    if (!r.writes.empty()) log_writes(log, sample, r, ok);
    // The step's baseline assumed its write landed; if it didn't, start over
    // rather than measure the next drop from a hull the game never held.
    if (!ok) state = ds::StepState{};

    // A new boat (a mission start or a save-load) is logged once.
    if (r.baselined && sample.boat != last_boat) {
      log.logf("[depth-stress] baseline: boat 0x%08lX, hull %.2f / %.2f, "
               "clock %ld s, CD %.2f, CS %.4f",
               static_cast<unsigned long>(sample.boat), sample.hull,
               sample.max_hull, static_cast<long>(sample.clock_s),
               sample.crush_field, sample.crush_speed);
      last_boat = sample.boat;
    }

    // No seep compartment is logged when the reason changes, not every poll.
    if (have) {
      const char* seep_why = ape::game::seep_unavailable();
      if (seep_why != last_seep_why) {
        if (seep_why)
          log.logf("[depth-stress] no seepage: %s", seep_why);
        else
          log.logf("[depth-stress] seep compartment found");
        last_seep_why = seep_why;
      }
    }

    if (!have)
      skips.skipped(why);
    else if (r.skipped)
      skips.skipped(r.skipped);
    else
      skips.ran();
    Sleep(kPollMs);
  }
  return 0;
}

}  // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID /*reserved*/) {
  switch (reason) {
    case DLL_PROCESS_ATTACH: {
      DisableThreadLibraryCalls(module);
      g_loaded = ds::read_deployed_constants();
      g_verdict = ds::verify(g_loaded);
      const HANDLE thread =
          CreateThread(nullptr, 0, depth_stress_thread, module, 0, nullptr);
      if (thread) CloseHandle(thread);
      break;
    }
    case DLL_PROCESS_DETACH:
      // Never wait here: waiting on a thread under the loader lock can
      // deadlock. The thread pinned the module, so it's safe to leave it.
      g_stop.store(true);
      break;
    default:
      break;
  }
  return TRUE;
}
