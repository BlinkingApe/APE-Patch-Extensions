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

# --- Static C/C++ runtime (/MT, /MTd) --------------------------------------
# ape.act is loaded into sh3.exe on players' machines, so it must not depend on
# the Visual C++ Redistributable. Linked dynamically it needs MSVCP140.dll and
# VCRUNTIME140.dll (x86): if they're missing, LoadLibrary fails at game start,
# and if the process loads an msvcp140.dll older than 14.40, std::mutex
# (constexpr since VS 2022 17.10) crashes in _Mtx_lock on the logger's first
# write. Linking the runtime in leaves KERNEL32.dll as the only import. Every
# target gets it, so the static libraries and tests agree with ape.act.
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

# --- Vendored dependencies ------------------------------------------------
# MinHook was dropped in the architecture pivot: APE runs alongside and never
# hooks a stock function (ADR-0001), so no function-hooking library is linked.

# --- core: infra shared across every mod (logging today; AOB scanning and a
# generic config reader land here as more mods need them — see ticket 02,
# .scratch/umbrella-roadmap/). Header-only for now (src/core/log.hpp), so no
# library target — every target below gets `src` on its include path instead.

# --- depth_stress_core: the pure depth-stress code -------------------------
# The constants struct's layout and verdict, and the step function (PRD seam
# 1). No I/O, no Windows headers, no game memory: both ape.act and the test
# binary link it.
add_library(depth_stress_core STATIC
  src/mods/depth_stress/constants.cpp
  src/mods/depth_stress/step.cpp
  src/mods/depth_stress/warning.cpp
)
target_include_directories(depth_stress_core PUBLIC src)

# --- ape.act: the injected module (PE32 DLL, `.act` extension) -----------
# DllMain verifies the depth-stress constants struct (ADR-0006) and starts one
# background thread that reads game memory, steps and writes; nothing is
# hooked (ADR-0001).
add_library(ape_act SHARED
  src/ape_act/dllmain.cpp
  src/ape_act/depth_stress_constants.cpp
  src/ape_act/game_memory.cpp
)
target_include_directories(ape_act PRIVATE src)
target_link_libraries(ape_act PRIVATE depth_stress_core)

# The Release ape.act is the patcher's template (ADR-0006), so a Release build
# records its constants struct's file offset in the manifest. A Debug build
# lays the file out differently and leaves the manifest alone.
if(CMAKE_BUILD_TYPE STREQUAL "Release")
  find_package(Python3 COMPONENTS Interpreter)
  if(Python3_FOUND)
    add_custom_command(TARGET ape_act POST_BUILD
      COMMAND Python3::Interpreter
              "${CMAKE_SOURCE_DIR}/tools/re-static/gen_manifest.py"
              --ape-act "$<TARGET_FILE:ape_act>"
      COMMENT "Recording ape.act's constants struct offset in the manifest"
      VERBATIM)
  else()
    message(WARNING "No Python 3: the manifest's ape.act offsets won't be "
                    "updated by this build; run tools/re-static/gen_manifest.py")
  endif()
endif()
set_target_properties(ape_act PROPERTIES
  PREFIX ""            # no "lib" prefix
  OUTPUT_NAME "ape"
  SUFFIX ".act"        # loaded by OAE sh3.exe's custom .act loader
)

# --- patcher: standalone tool that inserts the ape.act load call site -----
add_executable(patcher
  src/patcher/main.cpp
)

# --- Tests ----------------------------------------------------------------
# Seam 1: the pure depth-stress code, under vendored doctest.
enable_testing()
add_executable(ape_tests
  tests/depth_stress_test.cpp
  tests/flooding_warning_test.cpp
)
target_link_libraries(ape_tests PRIVATE depth_stress_core)
target_include_directories(ape_tests PRIVATE third_party/doctest)
add_test(NAME ape_tests COMMAND ape_tests)
