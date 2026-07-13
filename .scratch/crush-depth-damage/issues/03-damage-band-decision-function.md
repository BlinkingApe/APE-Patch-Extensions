# Damage-band decision function + unit tests

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

The pure damage-band decision function: given current depth expressed as a percentage under crush depth, plus the light/medium/heavy band configuration (window boundaries and flat damage-per-tick rates), return which band applies and what damage rate to use — or a signal that depth is past the last configured band and stock behavior should apply instead. No I/O, no game-memory access. This is the one automated-test seam agreed for this hack.

## Acceptance criteria

- [ ] The function correctly selects the light, medium, or heavy band for depths within each respective window.
- [ ] Boundary depths (exactly at a window edge) resolve to the correct adjoining band, consistently.
- [ ] Depth past the outer edge of the heavy band returns a "defer to stock" signal rather than fabricating a rate.
- [ ] Depth at or above crush depth (zero percent under) is handled correctly.
- [ ] A degenerate config (a band with zero width) is handled without crashing or misclassifying.
- [ ] Unit tests (Catch2 or doctest, building under CMake/MSVC) cover all of the above and pass.

## Blocked by

None — pure logic, no RE dependency, no game interaction. Can start immediately.

## Comments

**False start (Linux sandbox session) — restart on Windows 11:** Implementation was attempted in a Linux sandbox and did not get past toolchain discovery — no CMake/g++/clang toolchain is available in that environment. No source files were created under `src/ape/`; nothing was left to clean up. Restart this ticket from scratch on Windows 11, using VS Code + CMake + MSVC per the project's build-toolchain decision. Still open when picking this back up: whether to vendor doctest (single-header) or fetch Catch2 via CMake — decide based on what's easiest to pull in cleanly on the actual dev machine, network access there hasn't been checked either.
