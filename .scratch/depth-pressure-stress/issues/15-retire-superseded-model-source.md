# Retire the superseded band-decision model's C++ implementation and build wiring

Type: task
Status: open
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

## Question

Ticket 13 deleted the four dead *ticket* files (`03`/`04`/`06`/`07`) describing the
abandoned three-band, fraction-of-crush-depth model, and amended the docs that
described it in prose. It did not touch the actual C++ that implements that model,
which is still built and wired in:

- `src/mods/crush_depth_damage/{config,damage_band}.{hpp,cpp}` — the dead
  `select_band` function and its `[CrushDepthDamage]` INI config loader.
- `CMakeLists.txt` — builds the above into a library, presumably linked into
  `ape_act` and/or `ape_core`.
- `tools/config_check/main.cpp` — a standalone harness that self-checks the dead
  config shape and prints the parsed values of the shipped `data/Cfg/ape.cfg`.
- `tests/damage_band_test.cpp` — doctest coverage of `select_band`'s boundary
  behavior.
- `data/Cfg/ape.cfg` — the shipped config file, in the dead `[CrushDepthDamage]`
  INI shape.
- `src/ape_act/dllmain.cpp`, `src/ape_act/damage_loop.{hpp,cpp}`,
  `src/ape_act/game_state.hpp` — the "offline scaffold" from old ticket `05`
  (amended 2026-08-17, see that ticket): the loop-thread/logging skeleton is
  reusable, but its damage-application call (`select_band`, writing `hp -= rate *
  dt`) is dead-model-specific.

This wasn't deleted alongside `03`/`04` because it's live in the build graph —
removing it risks breaking the build if done carelessly (unlinking a CMake target,
orphaning a test target, etc.), which is why it's a separate ticket rather than
folded into `13`'s bookkeeping pass.

Two sub-questions:

1. **Delete outright, or gut and keep the shell?** `select_band`/`config.cpp` are
   pure dead code (no reuse). `damage_loop.{hpp,cpp}`/`dllmain.cpp` have a reusable
   *skeleton* (thread spawn, fail-inert seam, plausibility gates, logger) with a
   dead *payload* (the band-model damage application). Decide whether to delete the
   whole `src/mods/crush_depth_damage/` tree now and let a future ticket rebuild the
   scaffold against the tier model from scratch, or keep the reusable skeleton files
   in place with their dead call sites clearly marked, ready for the tier model to
   drop in.
2. **What happens to `data/Cfg/ape.cfg` and `tools/config_check`?** Both assume the
   `[CrushDepthDamage]` shape ADR-0006 has already superseded (constants now live in
   a magic-marked struct in `ape.act`'s own `.data`, deployed by the patcher, not an
   INI file). Confirm with ADR-0006 whether `ape.cfg` has any surviving purpose
   before deciding whether to delete or repurpose it.

## Acceptance criteria

- [ ] Decision recorded on both sub-questions above.
- [ ] The build (`ape_core`, `ape_act`, `ape_tests`, `config_check` — whichever
      survive the decision) still compiles clean via CMake/MSVC after the change.
- [ ] `ctest`/`ape_tests` still green (with the dead `damage_band_test.cpp` removed
      or updated to match whatever remains).
- [ ] No file in the tree still describes or implements `select_band`,
      `BandConfig`, or the `[CrushDepthDamage]` INI shape as live — either deleted,
      or explicitly marked dead/historical with no live call path.
