cmake_minimum_required(VERSION 3.21)
project(ape LANGUAGES CXX C)

# --- Hard constraint: APE targets the 32-bit OAE sh3.exe. -----------------
# ape.act must be a PE32 (x86) module. Building x64 would produce a DLL the
# target process cannot load. Fail loudly at configure time rather than at
# runtime. Configure inside an x86 developer environment (vcvars32.bat) or
# select the x86 kit in the VS Code CMake Tools extension.
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 4)
  message(FATAL_ERROR
    "APE must be built as 32-bit (x86). Detected a "
    "${CMAKE_SIZEOF_VOID_P}-byte pointer (x64). "
    "Configure from an x86 dev shell (vcvars32.bat) or pick the x86 kit.")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# --- Vendored dependencies ------------------------------------------------
# MinHook was dropped in the architecture pivot: APE runs alongside and never
# hooks a stock function (ADR-0001), so no function-hooking library is linked.

# --- core: infra shared across every mod (logging today; AOB scanning and a
# generic config reader land here as more mods need them — see ticket 02,
# .scratch/umbrella-roadmap/). Header-only for now (src/core/log.hpp), so no
# library target — every target below gets `src` on its include path instead.

# --- ape.act: the injected module (PE32 DLL, `.act` extension) -----------
# DllMain confirms it loaded; no mods are wired in yet. The three-band
# crush-depth damage loop that used to live here was retired by ticket 15
# (ADR-0008 reversed the model it implemented) — a future mod wires its own
# loop in from dllmain.cpp once the tier-model RE/PRD work lands.
add_library(ape_act SHARED
  src/ape_act/dllmain.cpp
)
target_include_directories(ape_act PRIVATE src)
set_target_properties(ape_act PROPERTIES
  PREFIX ""            # no "lib" prefix
  OUTPUT_NAME "ape"
  SUFFIX ".act"        # loaded by OAE sh3.exe's custom .act loader
)

# --- patcher: standalone tool that inserts the ape.act load call site -----
add_executable(patcher
  src/patcher/main.cpp
)
