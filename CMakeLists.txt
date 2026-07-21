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

# --- ape_core: the pure, unit-testable logic ------------------------------
# No I/O, no Windows headers, no game memory. Both ape.act and the test
# binary link this. This is the one seam covered by automated tests.
add_library(ape_core STATIC
  src/core/damage_band.cpp
)
target_include_directories(ape_core PUBLIC src)

# --- ape_config: ape.cfg (INI) loading ------------------------------------
# File parsing lives out of ape_core so ape_core keeps its "no I/O" contract.
# Per the spec, real-file config parsing is not part of the automated test
# seam; it is verified manually via the config_check harness below.
add_library(ape_config STATIC
  src/core/config.cpp
)
target_include_directories(ape_config PUBLIC src)

# --- ape.act: the injected loop module (PE32 DLL, `.act` extension) -------
# Spawns the crush-depth damage loop thread from DllMain and runs it alongside
# the game (no function hooks). The live-value resolver (issue 01) plugs in
# behind the IGameState seam.
add_library(ape_act SHARED
  src/ape_act/dllmain.cpp
  src/ape_act/damage_loop.cpp
)
target_link_libraries(ape_act PRIVATE ape_core ape_config)
set_target_properties(ape_act PROPERTIES
  PREFIX ""            # no "lib" prefix
  OUTPUT_NAME "ape"
  SUFFIX ".act"        # loaded by OAE sh3.exe's custom .act loader
)

# --- patcher: standalone tool that inserts the ape.act load call site -----
add_executable(patcher
  src/patcher/main.cpp
)

# --- config_check: manual verification harness for the ape.cfg loader -----
# Deliberately NOT registered with ctest (config parsing is manually verified,
# per the spec's testing decision). Run it by hand, optionally passing the path
# to data/Cfg/ape.cfg to print the shipped values.
add_executable(config_check
  tools/config_check/main.cpp
)
target_link_libraries(config_check PRIVATE ape_config)

# --- Tests ----------------------------------------------------------------
enable_testing()
add_executable(ape_tests
  tests/damage_band_test.cpp
)
target_link_libraries(ape_tests PRIVATE ape_core)
target_include_directories(ape_tests PRIVATE third_party/doctest)
add_test(NAME ape_tests COMMAND ape_tests)
