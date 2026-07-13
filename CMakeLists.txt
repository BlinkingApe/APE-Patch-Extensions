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
add_subdirectory(third_party/minhook)  # provides the `minhook` static lib

# --- ape_core: the pure, unit-testable logic ------------------------------
# No I/O, no Windows headers, no game memory. Both ape.act and the test
# binary link this. This is the one seam covered by automated tests.
add_library(ape_core STATIC
  src/core/damage_band.cpp
)
target_include_directories(ape_core PUBLIC src)

# --- ape.act: the injected hook module (PE32 DLL, `.act` extension) -------
add_library(ape_act SHARED
  src/ape_act/dllmain.cpp
)
target_link_libraries(ape_act PRIVATE ape_core minhook)
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
enable_testing()
add_executable(ape_tests
  tests/damage_band_test.cpp
)
target_link_libraries(ape_tests PRIVATE ape_core)
target_include_directories(ape_tests PRIVATE third_party/doctest)
add_test(NAME ape_tests COMMAND ape_tests)
