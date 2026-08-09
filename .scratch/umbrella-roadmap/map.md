# Umbrella roadmap: multi-mod repo structure + candidate goals

Label: wayfinder:map

## Destination

A repo (`src/`, `.scratch/`, `docs/`) reorganized to hold multiple mods/subsystems
under one shared `ape.act` runtime — extensible for future growth — **and** each
of the currently-known candidate goals (hull-repair patch, diesel-damage patch,
docking mod) scoped precisely enough to spin up its own feature effort (PRD +
issues dir), the same way `crush-depth-damage` already works. Reaching the end
means: the layout is decided and applied, and nothing is left to decide before
each new goal can start its own implementation-issue tracker.

## Notes

- Domain: OAE sh3.exe / `.act` DLL modding — see `CONTEXT.md` at repo root.
- One shared `ape.act` runtime, many subsystems (decided) — not separate DLLs
  per mod.
- Existing precedent for a fully-specified mod effort: `.scratch/crush-depth-damage/`
  (PRD.md + numbered issues). New goals should land in a directory of the same
  shape once scoped.
- Priority across new goals (hull-repair, diesel-damage, docking) is
  unordered/opportunistic — no fixed sequence.
- Use `/grilling` and `/domain-modeling` for tickets on this map; use
  `/to-spec` or `/to-tickets` once a goal's ticket resolves, to spin up its own
  feature effort under `.scratch/<feature-slug>/`.

## Decisions so far

- [Repo layout for a multi-mod umbrella](issues/01-repo-layout-for-multi-mod-umbrella.md) —
  `src/mods/<mod_name>/` per subsystem, shared infra in `src/core/`, DLL
  entrypoint stays in `src/ape_act/`; `.scratch/` and `docs/` unchanged.
  Applied: `crush-depth-damage` moved into `src/mods/crush_depth_damage/`,
  `log.hpp` promoted to `src/core/`, CMake targets renamed
  (`crush_depth_damage_core`/`crush_depth_damage_config`). Build + tests +
  `config_check` verified green.

## Not yet specified

- **Pseudo-dock mod (item 4).** Docking triggers "other events in-game" — TBD,
  too open-ended to ticket yet. Revisit once the repo layout and config
  strategy are decided; may need its own grilling/prototype session to even
  name the candidate trigger events.
- **Future/unnamed sub-projects.** The user wants the structure extensible for
  goals not yet conceived. No specific ticket — the layout ticket (01) should
  produce a convention general enough to absorb these without another reorg.

## Out of scope

(none yet)
