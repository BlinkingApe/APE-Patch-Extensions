# Damage-band decision function + unit tests

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

The pure damage-band decision function: given current depth expressed as a percentage under crush depth, plus the light/medium/heavy band configuration (window boundaries and flat damage-per-tick rates), return which band applies and what damage rate to use — or a signal that depth is past the last configured band and stock behavior should apply instead. No I/O, no game-memory access. This is the one automated-test seam agreed for this hack.

## Acceptance criteria

- [x] The function correctly selects the light, medium, or heavy band for depths within each respective window.
- [x] Boundary depths (exactly at a window edge) resolve to the correct adjoining band, consistently.
- [x] Depth past the outer edge of the heavy band returns a "defer to stock" signal rather than fabricating a rate.
- [x] Depth at or above crush depth (zero percent under) is handled correctly.
- [x] A degenerate config (a band with zero width) is handled without crashing or misclassifying.
- [x] Unit tests (Catch2 or doctest, building under CMake/MSVC) cover all of the above and pass.

## Blocked by

None — pure logic, no RE dependency, no game interaction. Can start immediately.

## Comments

**False start (Linux sandbox session) — restart on Windows 11:** Implementation was attempted in a Linux sandbox and did not get past toolchain discovery — no CMake/g++/clang toolchain is available in that environment. No source files were created under `src/ape/`; nothing was left to clean up. Restart this ticket from scratch on Windows 11, using VS Code + CMake + MSVC per the project's build-toolchain decision. Still open when picking this back up: whether to vendor doctest (single-header) or fetch Catch2 via CMake — decide based on what's easiest to pull in cleanly on the actual dev machine, network access there hasn't been checked either.

**COMPLETE (Windows 11, commit `1acf9be`).** Implemented as the pure `ape::select_band` in `src/core/damage_band.{hpp,cpp}` (the `ape_core` static lib), with all six acceptance criteria covered by `tests/damage_band_test.cpp`. Decision: **doctest** vendored as a single header under `third_party/doctest/` (network was available; simpler than fetching Catch2). Tests build and pass under CMake/MSVC x86 via `ctest --preset x86-debug`. Note on the boundary test: boundaries are exercised with binary-exact fractions (1/8, 1/4, 1/2) so "exactly on the edge" is deterministic rather than a floating-point coin flip. The band-config struct shape (`ape::BandConfig`: three `*_window` fractions + three `*_rate` values, cumulative half-open intervals) is now fixed — this is what issue 04's config loader must produce.
