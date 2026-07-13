# Crush-Depth Damage Model (APE)

Status: ready-for-agent

## Problem Statement

Past crush depth, OAE sh3.exe applies hull damage as a single linear value — the only way to tune it is one flat multiplier exposed in an existing config file. This doesn't let a player/modder express "a brief overshoot is forgivable, but staying deep gets punishing fast": there's no way to make damage escalate as depth increases past the threshold, only a uniform rate regardless of how far under crush depth the submarine is.

## Solution

APE hooks the crush-depth damage calculation in OAE sh3.exe and replaces the linear model with three configurable, flat-rate damage bands (light/medium/heavy), each defined as a percentage-of-crush-depth window with its own fixed damage-per-tick rate. Crossing from one band to the next is a step change, not a ramp. Past the outer edge of the heavy band, stock damage behavior takes over unmodified — APE does not own the terminal-failure zone. All values are tunable via a config file without recompiling.

## User Stories

1. As an OAE player, I want submarine hull damage to increase the deeper I go past crush depth, so that risking depth feels appropriately dangerous rather than uniform.
2. As an OAE player, I want a light-damage band just past crush depth, so that a brief, shallow overshoot isn't instantly catastrophic.
3. As an OAE player, I want damage to escalate through medium and heavy bands the deeper I go, so that pushing dangerously deep is clearly riskier than a shallow overshoot.
4. As an OAE player, I want the depth thresholds to scale with each submarine class's own crush depth, so that the damage model feels consistent across different boat types rather than using one fixed depth for all classes.
5. As an OAE player, I want all other crush-depth-adjacent behavior (e.g. terminal hull failure past the heavy band) to remain exactly as OAE already defines it, so that the hack changes only the intended zone and doesn't introduce unrelated regressions.
6. As an APE maintainer, I want the crush-depth damage function's address resolved via AOB pattern scan, so that I don't have to manually re-find an offset every time OAE is rebuilt.
7. As an APE maintainer, I want a documented, versioned AOB signature per OAE build recorded in the repo, so that I can tell at a glance whether a given signature is still valid for the OAE version currently installed.
8. As an APE maintainer, I want band boundaries and per-band damage rates exposed in config, so that I can tune the damage curve without recompiling.
9. As an APE maintainer, I want the damage-band decision logic implemented as a pure, unit-testable function, so that I can verify boundary behavior (band edges, and the point where stock behavior resumes) without needing a running game.
10. As an APE maintainer, I want ape.act to log depth, percent-under-crush-depth, selected band, and applied damage rate on every tick the hook runs, so that I can cross-check in-game behavior against expected arithmetic instead of eyeballing the HUD.
11. As an APE maintainer, I want a way to force-set a submarine's depth to an exact test point (via a Cheat Engine script or prepared save), so that I can repeatably test each band boundary without a multi-minute manual dive each time.
12. As an APE maintainer, I want a standalone patcher tool that inserts the ape.act load call site into a given sh3.exe, so that I can reapply the patch quickly after every OAE rebuild instead of redoing it by hand in a debugger.
13. As an APE maintainer, I want the patcher tool to be safely re-runnable against a freshly rebuilt OAE sh3.exe, so that reapplying the patch after a rebuild is a routine, low-risk step rather than a delicate one-off.
14. As an APE maintainer, I want depth past the outer edge of the heavy band to fall back to stock crush-depth behavior unmodified, so that the hook's scope stays bounded and I don't have to reverse-engineer the terminal-failure logic.
15. As a future APE contributor, I want new hacks added as additional hook installs inside the same ape.act module, so that adding a hack never requires a new sh3.exe patch.
16. As a prospective APE user (OAE/SH3 modding community member), I want to install APE via JSGME by dropping in a single mod folder, so that installation matches the workflow I already use for every other OAE mod.
17. As a prospective APE user, I want to run a standalone patcher against my own legally-owned OAE sh3.exe, so that I never need to download or install a redistributed, modified copy of the executable.
18. As a prospective APE user, I want the config file documented with comments describing each key's meaning, units, and valid ranges, so that I can tune the damage model without reading source code.
19. As a prospective APE user, I want the crush-depth damage hack to live as one section within a single shared config file, so that as more APE hacks are released I only manage one file.
20. As an APE maintainer, I want damage bands defined as flat per-tick rates rather than interpolated/ramped values, so that the tuning model stays simple to reason about and simple to express in config.

## Implementation Decisions

- **`ape.act` module**: a single PE32 DLL under OAE sh3.exe's custom `.act` loader convention, containing all current and future APE hooks. `DllMain` installs a MinHook-based inline hook for this hack (and, in the future, any others).
- **`sh3.exe` patch**: applied by a standalone patcher tool that inserts one dedicated `LoadLibrary` call site loading `ape.act`. This is the only modification APE makes to `sh3.exe`, and it's shared across the entire hack suite — future hacks do not add further `sh3.exe` patches (ADR-0002).
- **Target function resolution**: AOB pattern scan performed at DLL load time, not a hardcoded offset. The signature is recorded and versioned per OAE build under `docs/re-notes/` (ADR-0003).
- **Hook implementation**: MinHook (vendored) installs the inline hook on the crush-depth damage function; the original function pointer is preserved (ADR-0001).
- **Damage-band decision logic**: a pure function — inputs are current depth, crush depth, and the band configuration (three boundaries + three rates); output is the selected band and the damage rate to apply this tick, or a signal that depth is past the last configured band and stock behavior should apply. No I/O, no game-memory access — this is the unit-tested seam (see Testing Decisions).
- **Band configuration shape**: three named bands — light, medium, heavy — each with a window boundary expressed as a fraction of crush depth, applied cumulatively (light covers `[0, light_boundary)`, medium covers `[light_boundary, light_boundary + medium_boundary)`, heavy covers the next `heavy_boundary` beyond that), and each with its own flat damage-per-tick rate. Beyond the outer edge of the heavy band, the hook does not intervene.
- **Config**: a single `data/Cfg/ape.cfg`, INI format, with a `[CrushDepthDamage]` section holding descriptive keys for the three window boundaries and three damage rates (exact key names finalized during implementation, following the `light_damage_window` / `light_damage_rate` naming pattern already agreed). Parsed once at DLL load; no hot-reload (ADR-0004).
- **Logging**: on every tick the hook's damage logic runs, `ape.act` appends a line to a debug log recording current depth, percent-under-crush-depth, selected band, and damage rate applied.
- **Crush depth source**: read from existing engine-tracked state at the hook site; APE does not compute or own its own crush-depth value.
- **Distribution artifacts**: (1) a JSGME-installable mod folder containing `ape.act` and `data/Cfg/ape.cfg`; (2) the standalone patcher tool, used both by end users once at install time and internally after every OAE rebuild. Whether to also ship a pre-patched exe is deferred (ADR-0005).
- **Toolchain**: CMake + MSVC targeting Windows 11, built via VS Code; MinHook vendored as a dependency.

## Testing Decisions

- Exactly one seam is covered by automated tests: the pure damage-band decision function. It takes depth-as-percent-under-crush-depth plus the band configuration, and returns which band applies and what rate to use — no game process, no memory access, no file I/O involved, so it can run under a normal C++ test framework (Catch2 or doctest — final pick left to implementation) independent of a running game.
- Cases to cover: depth within each of the three bands; depth exactly at each band boundary; depth past the outer edge of the heavy band (must signal "defer to stock," not fabricate a rate); depth at or above crush depth exactly (zero percent under); and a degenerate config where a band is configured with zero width.
- Everything outside this seam — AOB scanning, MinHook installation, `DllMain`, config-file parsing against a real file, live memory reads of depth/crush depth, and the `sh3.exe` patch itself — is explicitly not covered by automated tests. It's verified manually: debug-log output cross-checked against expected arithmetic, using a Cheat Engine script or a prepared save to force the submarine to exact test depths.
- No prior art exists in this repo (greenfield project) — this spec establishes the first test seam and its convention for future hacks in the suite.

## Out of Scope

- Any hack beyond the crush-depth damage model — future hacks get their own spec.
- Ramped or interpolated damage within a band — flat rate only.
- Hot-reloading the config file at runtime.
- Owning or replacing the terminal hull-failure logic past the heavy band.
- Ghidra/IDA-based static analysis tooling — explicitly deferred.
- Deciding whether to ship a pre-patched `sh3.exe` alongside the patcher tool.
- Automated testing of hook plumbing, memory reads, config parsing, or the `sh3.exe` patch itself — manual/CE-based verification only, per the agreed seam.
- Support for stock `sh3.exe` or any mod pack other than OAE.

## Further Notes

- This is the first hack built under the APE project. Its architecture (single `ape.act` module, single `sh3.exe` patch, AOB scanning, single sectioned config file) is intended as the template future hacks build on top of — see `docs/adr/0001` through `0005`.
- Full context and terminology: `CONTEXT.md` at the repo root.
- Facts about the OAE install referenced here (module formats, `hsie.act`/`sh5.act` loading behavior) were established by direct inspection of the actual install during the planning session that produced this spec; see ADR-0002 for details.
