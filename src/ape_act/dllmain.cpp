// ape.act entry point.
//
// Scaffold: on load, initialize MinHook and (in future) install the suite's
// inline hooks; on unload, tear them down. The crush-depth damage hook itself
// — AOB scan for the target function, MH_CreateHook, and the DllMain wiring —
// lands under .scratch/crush-depth-damage/issues/05. This file exists now to
// prove the module builds as a PE32 DLL and links the vendored MinHook.

#include <windows.h>

#include "MinHook.h"

namespace {

bool install_hooks() {
  if (MH_Initialize() != MH_OK) {
    return false;
  }
  // TODO(issue-05): AOB-scan the crush-depth damage function, create and
  // enable the inline hook that routes it through ape::select_band.
  return true;
}

void remove_hooks() {
  MH_DisableHook(MH_ALL_HOOKS);
  MH_Uninitialize();
}

}  // namespace

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID /*reserved*/) {
  switch (reason) {
    case DLL_PROCESS_ATTACH:
      DisableThreadLibraryCalls(module);
      install_hooks();
      break;
    case DLL_PROCESS_DETACH:
      remove_hooks();
      break;
    default:
      break;
  }
  return TRUE;
}
