# Repo layout for a multi-mod umbrella

Type: grilling
Status: resolved

## Blocked by

None

## Question

APE currently ships one subsystem (crush-depth damage) inside one `ape.act`
runtime, with `src/` split by role (`ape`, `ape_act`, `core`, `patcher`) rather
than by mod, and `.scratch/` split one-directory-per-feature
(`.scratch/depth-pressure-stress/`). The umbrella now needs to hold several
subsystems (crush-depth damage, hull-repair patch, diesel-damage patch,
docking mod, and unnamed future ones) inside the *same* shared `ape.act`
runtime.

Decide the convention for:

- **`src/`**: where each subsystem's code lives relative to the existing
  role-based split (`ape`, `ape_act`, `core`, `patcher`) — e.g. a `subsystems/`
  or `mods/` directory with one folder per mod, vs. keeping role-based dirs and
  adding files per mod, vs. some hybrid. Must support each subsystem owning its
  own AOB anchors/config bindings/loop wiring while sharing `core` infra
  (config loading, logging, AOB scanning).
- **`.scratch/`**: whether the existing one-feature-per-directory convention
  (`docs/agents/issue-tracker.md`) stays as-is per subsystem, or needs
  adjustment now that subsystems share one runtime instead of being fully
  independent efforts.
- **`docs/`**: whether `docs/adr/` and `CONTEXT.md` stay single/shared across
  all subsystems, or need per-subsystem sections/subdirectories as the count
  grows.

Answer should be concrete enough to apply immediately (rename/move the
existing `crush-depth-damage` code and docs into the new shape as part of
resolving this ticket, or as an immediate follow-up task) and general enough
to absorb goals not yet conceived.

## Answer

**`src/`**: added `src/mods/<mod_name>/` — one folder per subsystem, holding
that subsystem's hook wiring, loop orchestration, and config bindings. Shared
infra stays in `src/core/` (currently just `log.hpp`; AOB scanning and a
generic config reader land there once ticket 02 and a second mod's RE work
give them a real shared shape — not invented speculatively now). The DLL
entrypoint (`src/ape_act/dllmain.cpp`) stays put and wires each mod's
init/loop in; it does not move into `mods/`.

Applied immediately: `crush-depth-damage`'s files moved from `src/core/` and
`src/ape_act/` into `src/mods/crush_depth_damage/`
(`damage_band.{hpp,cpp}`, `config.{hpp,cpp}`, `damage_loop.{hpp,cpp}`,
`game_state.hpp`); `log.hpp` moved from `src/ape_act/` to `src/core/` since it
was already mod-agnostic. Header guards and `#include` paths updated
throughout (`dllmain.cpp`, `tests/damage_band_test.cpp`,
`tools/config_check/main.cpp`). CMake targets renamed to match ownership:
`ape_core` → `crush_depth_damage_core`, `ape_config` → `crush_depth_damage_config`
(both now sourced from `src/mods/crush_depth_damage/`); `ape_act` links
against them and includes `src/mods/crush_depth_damage/damage_loop.cpp`
directly (future mods add their own loop `.cpp` to the same target the same
way, until/unless a mod needs its own DLL — out of scope, decided against in
this map's Notes). Full rebuild (`cmake --preset x86-debug` +
`cmake --build`) and `ctest` verified green; `config_check` still loads
`data/Cfg/ape.cfg` correctly. `docs/re-notes/01-live-session-playbook.md`'s
pointer to the not-yet-written live game-state resolver updated to the new
path.

**`.scratch/`**: kept as-is — one directory per feature effort
(`docs/agents/issue-tracker.md`'s existing convention). Planning-level, not
runtime-level; multiple mods sharing one DLL doesn't change how their
features are tracked.

**`docs/`**: kept as-is — `docs/adr/` and `CONTEXT.md` stay shared/repo-wide.
Revisit only if a mod's domain vocabulary outgrows `CONTEXT.md` (not the case
yet for any of the three candidate goals).
