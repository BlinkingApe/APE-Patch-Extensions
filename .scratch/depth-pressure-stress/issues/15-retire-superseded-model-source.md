# Retire the superseded band-decision model's C++ implementation and build wiring

Type: task
Status: resolved
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

- [x] Decision recorded on both sub-questions above.
- [x] The build (`ape_core`, `ape_act`, `ape_tests`, `config_check` — whichever
      survive the decision) still compiles clean via CMake/MSVC after the change.
- [x] `ctest`/`ape_tests` still green (with the dead `damage_band_test.cpp` removed
      or updated to match whatever remains).
- [x] No file in the tree still describes or implements `select_band`,
      `BandConfig`, or the `[CrushDepthDamage]` INI shape as live — either deleted,
      or explicitly marked dead/historical with no live call path.

## Answer

Resolved 2026-08-17.

**Sub-question 1 — deleted the whole tree, did not keep a shell.** The
"reusable skeleton" (thread spawn, `IGameState` seam, plausibility gates,
discontinuity guard, logger) turned out not to be cleanly separable from the
dead payload in the actual code: `damage_loop.cpp` inlines `select_band` and
the `rate · dt` subtraction directly in its main loop, and `game_state.hpp`'s
`Readings` carries a `crush_depth` field ADR-0008 says APE no longer reads at
all. The tier model's real mechanism — poll hull HP, infer the delta since
last tick, amplify it — is structurally different from "select a band, then
subtract a computed rate," so extracting a "clean" skeleton now would mean
guessing at a shape informed by a spec that doesn't exist yet (tickets
`08`–`12` and the PRD rewrite are still open). Ticket `05`'s own amendment
already deferred that rewiring to a fresh ticket once the tier model is
specced; half-extracting a shell here would just produce more code a future
session has to evaluate and probably discard. Deleted outright:
`src/mods/crush_depth_damage/{config,damage_band,damage_loop}.{hpp,cpp}`,
`game_state.hpp`.

**Sub-question 2 — `ape.cfg` and `config_check` deleted, not repurposed.**
ADR-0006 already calls this code dead and says to expect deletion once its
marked-struct replacement lands. That replacement is itself unimplemented
(ADR-0006's own status note), but that doesn't keep this code alive: its only
reason to exist was parsing the `[CrushDepthDamage]` INI shape, and that shape
belongs to a model ADR-0008/ticket `13` already killed. Deleted
`data/Cfg/ape.cfg` and `tools/config_check/main.cpp`; `data/Cfg/` removed
(now empty).

**Also deleted:** `tests/damage_band_test.cpp` (the automated-test seam for
`select_band`, which no longer exists).

**`src/ape_act/dllmain.cpp` rewritten** to a minimal `DllMain`: no thread, no
loop, just `DisableThreadLibraryCalls` and a log line confirming the DLL
loaded, so ADR-0002's open question ("does the call site tolerate `ape.act`
running with no live work?") stays checkable in-game. A future mod wires its
own loop thread in here once the tier model is specced.

**`CMakeLists.txt` updated:** removed the `crush_depth_damage_core` and
`crush_depth_damage_config` static-lib targets, the `config_check` executable,
and the `ape_tests`/`enable_testing()`/`ctest` wiring (no pure seam remains to
test). `ape_act` now builds from `dllmain.cpp` alone. `patcher` untouched.

**Verified:** built clean from an x86 (`vcvars32`) shell via
`cmake --build build\x86-debug` — `ape_act` and `patcher` link with no
errors or warnings; no `ape_tests`/`config_check` targets remain in the
generated build. Confirmed no remaining reference to `select_band`,
`BandConfig`, `CrushDepthDamage`, `crush_depth_damage_core`,
`crush_depth_damage_config`, or `damage_band` anywhere under `src/`,
`tools/`, `tests/`, `data/`, or `CMakeLists.txt`. The handful of remaining
matches repo-wide are all in `.scratch/`/`docs/adr/` prose describing the
dead model historically (tickets `05`/`13`/`15` themselves, ADR-0004, and two
umbrella-roadmap docs) — none assert it as live, so none needed touching.

**Not touched, out of scope:** `.scratch/depth-pressure-stress/PRD.md` still
describes the old model in the present tense. That's the map's own tracked
destination (`.scratch/depth-pressure-stress/map.md` "Destination"), not this
ticket's — the PRD rewrite waits on tickets `08`–`12` resolving first.
