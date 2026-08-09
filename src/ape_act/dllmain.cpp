// ape.act entry point (post-pivot: alongside, no function hooks).
//
// On attach, DllMain spawns a background loop thread and returns immediately —
// no AOB scanning or memory access happens under the loader lock (PRD). The
// thread loads ape.cfg, then runs the crush-depth damage loop against the
// game-state seam. On detach it signals the thread to stop and joins it.
//
// MinHook is gone: APE no longer hooks any stock function (ADR-0001). The live
// game-state resolver is issue 01's deliverable; until then UnresolvedGameState
// makes the loop fail inert (see game_state.hpp).

#include <windows.h>

#include <atomic>
#include <string>

#include "core/log.hpp"
#include "mods/crush_depth_damage/config.hpp"
#include "mods/crush_depth_damage/damage_band.hpp"
#include "mods/crush_depth_damage/damage_loop.hpp"
#include "mods/crush_depth_damage/game_state.hpp"

namespace {

std::atomic<bool> g_stop{false};
HANDLE g_thread = nullptr;

// Directory of the host process image (the game root), used to resolve the
// config and log paths at their real production locations.
std::string game_root() {
  char buf[MAX_PATH] = {0};
  const DWORD n = GetModuleFileNameA(nullptr, buf, MAX_PATH);
  std::string path(buf, n);
  const std::size_t slash = path.find_last_of("\\/");
  return slash == std::string::npos ? std::string() : path.substr(0, slash);
}

DWORD WINAPI loop_thread(LPVOID) {
  const std::string root = game_root();
  ape::Logger log(root + "\\ape_act.log");

  ape::BandConfig cfg{};
  std::string error;
  if (!ape::load_band_config(root + "\\data\\Cfg\\ape.cfg", cfg, error)) {
    log.logf("[config] failed to load ape.cfg: %s -- loop will not run", error.c_str());
    return 0;
  }
  log.logf("[config] loaded: light(%.4f/%.4f) medium(%.4f/%.4f) heavy(open/%.4f)",
           cfg.light_window, cfg.light_rate, cfg.medium_window, cfg.medium_rate,
           cfg.heavy_rate);

  // TODO(issue-01): replace with the real AOB-anchored resolver. Until then the
  // loop runs and fails inert (no anchors -> no damage), which is the correct
  // fail-safe for an un-wired build.
  ape::UnresolvedGameState game;
  ape::LoopLimits limits;
  ape::run_damage_loop(g_stop, game, cfg, limits, log);
  return 0;
}

}  // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID /*reserved*/) {
  switch (reason) {
    case DLL_PROCESS_ATTACH:
      DisableThreadLibraryCalls(module);
      g_stop.store(false);
      g_thread = CreateThread(nullptr, 0, loop_thread, nullptr, 0, nullptr);
      break;
    case DLL_PROCESS_DETACH:
      g_stop.store(true);
      if (g_thread) {
        // On process teardown (reserved != nullptr) the loader may have already
        // killed other threads; only wait when unloaded explicitly.
        WaitForSingleObject(g_thread, 2000);
        CloseHandle(g_thread);
        g_thread = nullptr;
      }
      break;
    default:
      break;
  }
  return TRUE;
}
