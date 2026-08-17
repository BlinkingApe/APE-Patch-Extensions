// ape.act entry point.
//
// The three-band, fraction-of-crush-depth damage loop that used to run from
// here is retired (ADR-0008, ticket 15): APE no longer owns crush-depth
// damage, and the tier model that replaces it (depth-proportional
// poll-and-amplify stress, seepage, engine wear) isn't specced yet — that
// waits on the live-value RE tickets and a rewritten PRD. Until a mod is
// wired back in here, DllMain does nothing but confirm it loaded.
//
// No function hooks (ADR-0001): whatever mod attaches next runs alongside the
// game from a background thread, never inline in a hooked function.

#include <windows.h>

#include <string>

#include "core/log.hpp"

namespace {

// Directory of the host process image (the game root), used to resolve the
// log path at its real production location.
std::string game_root() {
  char buf[MAX_PATH] = {0};
  const DWORD n = GetModuleFileNameA(nullptr, buf, MAX_PATH);
  std::string path(buf, n);
  const std::size_t slash = path.find_last_of("\\/");
  return slash == std::string::npos ? std::string() : path.substr(0, slash);
}

}  // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID /*reserved*/) {
  switch (reason) {
    case DLL_PROCESS_ATTACH: {
      DisableThreadLibraryCalls(module);
      ape::Logger log(game_root() + "\\ape_act.log");
      log.logf("[ape.act] loaded, no mods wired");
      break;
    }
    default:
      break;
  }
  return TRUE;
}
