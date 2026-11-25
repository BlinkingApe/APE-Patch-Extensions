// ape.act entry point: the loader (PRD Modules).
//
// DllMain copies the depth-stress constants struct out of ape.act's own .data
// and checks its marker and version (ADR-0006), a plain memory compare. It
// then starts one background thread and returns. Nothing else happens under
// the loader lock: no file I/O, no scanning, no game memory, and no function
// is hooked (ADR-0001).
//
// The thread does the logging. On a matching marker it logs the constants
// and runs the poll loop at about 10 Hz; on a mismatch it logs the reason and
// returns without ever entering the loop (fail inert). The loop does nothing
// yet: tickets 24-26 give it the reader, the step and the writer.

#include <windows.h>

#include <atomic>
#include <string>

#include "ape_act/depth_stress_constants.hpp"
#include "core/log.hpp"
#include "mods/depth_stress/constants.hpp"
#include "mods/depth_stress/step.hpp"

namespace ds = ape::depth_stress;

namespace {

constexpr DWORD kPollMs = 100;  // ~10 Hz; arithmetic runs on game time, not this

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

  ds::StepState state{};
  while (!g_stop.load()) {
    // No reader yet (ticket 24): every poll is "no sample".
    ds::step(g_loaded, state, nullptr);
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
