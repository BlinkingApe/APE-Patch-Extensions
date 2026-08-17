# Crush-Depth Damage Model (APE)

Status: ready-for-agent

## Problem Statement

Past crush depth, OAE sh3.exe applies hull damage as a single linear value — the only way to tune it is one flat per-unit rate (`CrashSpeed`) exposed in the submarine's data files. This doesn't let a player/modder express "a brief overshoot is forgivable, but staying deep gets punishing fast": there's no way to make damage escalate as depth increases past the threshold, only a uniform rate regardless of how far under crush depth the submarine is.

## Solution

APE runs its **own** crush-depth damage routine *alongside* OAE sh3.exe rather than intercepting the engine's damage logic. Following the hard-won convention of experienced developers in this space — never patch, route through, or otherwise interact with stock SH3 functions; write your own that live alongside — ape.act touches stock only by **reading and writing memory values**. It never hooks, replaces, or routes through an SH3 function.

The routine runs on ape.act's own loop thread. Each iteration it resolves the player boat, its current depth, the live crush depth (CDT), and hull HP; if the boat is below crush depth, it selects one of three configurable damage bands by *how far under CDT* the boat is, and subtracts that band's rate from hull HP directly. Crossing from one band to the next is a step change, not a ramp. APE owns the **entire** crush-depth damage model: stock's own `CrashSpeed` bleed is neutralized (set to 0 in the shipped per-unit data), so APE is the sole author of damage below crush depth. All values are tunable via a config file without recompiling.

## User Stories

1. As an OAE player, I want submarine hull damage to increase the deeper I go past crush depth, so that risking depth feels appropriately dangerous rather than uniform.
2. As an OAE player, I want a light-damage band just past crush depth, so that a brief, shallow overshoot isn't instantly catastrophic.
3. As an OAE player, I want damage to escalate through medium and into a catastrophic heavy band the deeper I go, so that pushing dangerously deep is clearly riskier than a shallow overshoot.
4. As an OAE player, I want the depth thresholds to scale with each submarine class's own crush depth, so that the damage model feels consistent across different boat types rather than using one fixed depth for all classes.
5. As an OAE player, I want damage to accrue in game-time — pausing or using time compression should not distort how much hull I lose for the game-time I actually spent deep — so that the model behaves the way stock damage intuitively would.
6. As an OAE player, I want APE never to unfairly destroy my boat because it misread the game's memory, so that a bad launch or a stale signature makes the hack do nothing rather than kill me.
7. As an APE maintainer, I want APE to own 100% of crush-depth damage (stock `CrashSpeed` neutralized), so that the damage curve is fully predictable and not the sum of APE plus whatever value the current OAE/mod set.
8. As an APE maintainer, I want the live values APE depends on (player boat, current depth, crush depth, hull HP, mission clock) resolved via AOB-anchored pointer chains scanned at runtime, so that I don't hand-maintain raw addresses and a patch update that shifts an anchor is *detected* (scan misses → fail inert) rather than silently misread.
9. As an APE maintainer, I want each resolved anchor's AOB signature and offset chain documented and versioned per OAE build in the repo, so that I can tell at a glance whether a given signature is still valid for the OAE version currently installed.
10. As an APE maintainer, I want band boundaries and per-band damage rates exposed in config, so that I can tune the damage curve without recompiling.
11. As an APE maintainer, I want the damage-band decision logic implemented as a pure, unit-testable function, so that I can verify boundary behavior (band edges, at-crush-depth) without needing a running game.
12. As an APE maintainer, I want ape.act to log depth, percent-under-crush-depth, selected band, applied damage rate, and the game-time delta on every loop iteration that applies damage, so that I can cross-check in-game behavior against expected arithmetic instead of eyeballing the HUD.
13. As an APE maintainer, I want a way to force-set a submarine's depth to an exact test point (via a Cheat Engine script or prepared save), so that I can repeatably test each band boundary without a multi-minute manual dive each time.
14. As an APE maintainer, I want a standalone patcher tool that inserts the ape.act load call site into a given sh3.exe, so that I can reapply the patch quickly after every OAE patch-suite reapplication instead of redoing it by hand in a debugger.
15. As an APE maintainer, I want the patcher tool to be safely re-runnable against an OAE sh3.exe that has had its patch suite reapplied, so that reapplying the ape.act load site is a routine, low-risk step rather than a delicate one-off.
16. As an APE maintainer, I want the heavy band to be open-ended and catastrophic (no separate "defer to stock" zone), so that a boat sitting far below crush depth keeps taking APE-owned damage and dies naturally rather than becoming invulnerable past a boundary.
17. As a future APE contributor, I want new hacks added as additional routines inside the same ape.act module, so that adding a hack never requires a new sh3.exe patch.
18. As a prospective APE user (OAE/SH3 modding community member), I want to install APE via JSGME by dropping in a single mod folder, so that installation matches the workflow I already use for every other OAE mod.
19. As a prospective APE user, I want to run a standalone patcher against my own legally-owned OAE sh3.exe, so that I never need to download or install a redistributed, modified copy of the executable.
20. As a prospective APE user, I want the config file documented with comments describing each key's meaning, units, and valid ranges, so that I can tune the damage model without reading source code.
21. As a prospective APE user, I want the crush-depth damage hack to live as one section within a single shared config file, so that as more APE hacks are released I only manage one file.
22. As an APE maintainer, I want damage bands defined as flat per-band rates rather than interpolated/ramped values, so that the tuning model stays simple to reason about and simple to express in config.

## Implementation Decisions

- **Architecture — alongside, not intercepting.** APE never hooks, replaces, or routes through a stock SH3 function. All runtime effect is achieved by reading and writing memory values. This is the founding principle for the whole hack suite.
- **`ape.act` module**: a single PE32 DLL under OAE sh3.exe's custom `.act` loader convention, containing all current and future APE hacks. It is loaded by a dedicated `LoadLibrary` call site patched into stock sh3.exe (ADR-0002); this load-site patch is the *only* modification APE makes to sh3.exe and is shared across the entire suite.
- **Execution**: `DllMain` spawns a background loop thread on attach and signals it to stop on detach. AOB scanning and all memory access happen on that thread, never under the DLL loader lock. No function-hooking machinery is used (MinHook dropped — see ADR-0001).
- **Damage model — three bands, heavy open-ended.** Bands are expressed as **windows of "fraction under crush depth"**: `under = (depth − CDT) / CDT`, clamped at 0. `light` covers `[0, a)`, `medium` covers `[a, b)`, `heavy` covers `[b, ∞)` — open-ended and catastrophic. There is no fourth band and no "defer to stock" zone: past the medium edge, the heavy rate applies until hull HP reaches 0 and the boat dies naturally.
- **Timing — HP per game-second, driven by the mission clock.** Rates are HP per game-second. Each iteration computes `dt = Δ(mission clock)` and applies `hp −= rate · dt`. This handles pause (clock frozen → `dt = 0` → no damage) and time compression (clock scales → `dt` scales) automatically, with the mission clock as the single value that captures both. Wall-clock is never used for damage math; the loop's poll frequency is a free implementation choice.
- **Neutralizing stock**: `CrashSpeed` is a per-unit data value, cached at load. APE ships a JSGME data edit setting `CrashSpeed` to 0 in each *playable* u-boat class's data file, so stock contributes nothing below crush depth and APE owns 100% of the damage. AI units use their own unedited files and keep stock behavior.
- **Scope**: APE damages the **player boat only** — a single object resolved each iteration.
- **Live-value resolution — AOB-anchored, self-verifying.** ape.act signature-scans stable stock sh3.exe for each anchor instruction, extracts the static operand it uses, and follows a pointer/offset chain to the live heap object. sh3.exe is the stock Ubisoft image (non-ASLR, at `0x00400000`) and is never recompiled — OAE/hsie/Stiebler/NSAP are byte-level patches reapplied over it — so signatures are reliable; the payoff of scanning over hardcoded addresses is *self-detection* (a patch that shifts an anchor makes the scan miss, which fails inert rather than silently misreading). Signatures + offset chains are versioned per OAE build under `docs/re-notes/` (ADR-0003). No `.act` module base needs resolving, since APE no longer touches the collision solver.
- **Fail-safe — fail inert.** If any required input (player boat, current depth, CDT, hull HP, mission clock) cannot be resolved or fails a plausibility gate, the iteration is skipped, the reason is logged, and no damage is applied. APE never guesses.
- **Concurrency**: the loop and the game simulation run on separate threads. APE reads hull HP fresh each iteration and writes it back (plain read-modify-write); an aligned float store is atomic on x86 and the per-iteration delta is small, so the cross-thread race is accepted rather than synchronized.
- **Damage-band decision logic**: a pure function — inputs are current depth, crush depth, and the band configuration (two boundaries + three rates); output is the selected band and the per-game-second damage rate to apply. No I/O, no game-memory access — this is the unit-tested seam (see Testing Decisions). The caller multiplies the returned rate by `dt` and subtracts from HP.
- **Band configuration shape**: three named bands — light, medium, heavy — with two cumulative window boundaries expressed as fractions of crush depth (`light_window`, `medium_window`; heavy is everything beyond) and each band's own flat HP-per-game-second rate.
- **Config**: a single `data/Cfg/ape.cfg`, INI format, with a `[CrushDepthDamage]` section (ADR-0004). Parsed once at DLL load; no hot-reload.
- **Logging**: on every iteration that applies damage, `ape.act` appends a line recording current depth, percent-under-crush-depth, selected band, damage rate, game-time delta, and HP applied.
- **Crush depth source**: read live from engine-tracked state each iteration; APE does not compute or own its own crush-depth value (mods randomize it, so a static value would be wrong).
- **Distribution artifacts**: (1) a JSGME-installable mod folder containing `ape.act`, `data/Cfg/ape.cfg`, and the per-unit `CrashSpeed`=0 data edits; (2) the standalone patcher tool, used both by end users once at install time and internally after every OAE patch-suite reapplication. Whether to also ship a pre-patched exe is deferred (ADR-0005).
- **Toolchain**: CMake + MSVC targeting Windows 11, built via VS Code.

## Testing Decisions

- Exactly one seam is covered by automated tests: the pure damage-band decision function. It takes depth and crush depth (its ratio is what matters) plus the band configuration, and returns which band applies and what per-game-second rate to use — no game process, no memory access, no file I/O — so it runs under a normal C++ test framework (doctest, vendored) independent of a running game.
- Cases to cover: depth within each of the three bands; depth exactly at each of the two band boundaries; depth far below crush depth (must resolve to the open-ended heavy band, not "defer" or fabricate); depth at or above crush depth exactly (zero percent under → light band); and a degenerate config where a band is configured with zero width (simply never selected).
- Everything outside this seam — AOB scanning, pointer-chain resolution, the loop thread, `DllMain`, config-file parsing against a real file, live memory reads/writes of depth/crush depth/HP/clock, and the `sh3.exe` load-site patch — is explicitly not covered by automated tests. It's verified manually: debug-log output cross-checked against expected arithmetic, using a Cheat Engine script or a prepared save to force the submarine to exact test depths.
- No prior art exists in this repo (greenfield project) — this spec establishes the first test seam and its convention for future hacks in the suite.

## Out of Scope

- Any hack beyond the crush-depth damage model — future hacks get their own spec.
- Ramped or interpolated damage within a band — flat rate only.
- Hot-reloading the config file at runtime.
- Applying APE's banded damage to AI submarines (player boat only; AI keeps stock behavior via its unedited data).
- Hooking, replacing, or routing through any stock SH3 function — the founding principle forbids it.
- Ghidra/IDA-based static analysis tooling — explicitly deferred.
- Deciding whether to ship a pre-patched `sh3.exe` alongside the patcher tool.
- Automated testing of the loop thread, memory reads/writes, config parsing, or the `sh3.exe` patch itself — manual/CE-based verification only, per the agreed seam.
- Support for stock `sh3.exe` (unmodified Ubisoft release) or any mod pack other than OAE.

## Further Notes

- This is the first hack built under the APE project. Its architecture (single `ape.act` module, single `sh3.exe` load-site patch, own-loop routines that only read/write memory, AOB-anchored value resolution, single sectioned config file) is intended as the template future hacks build on top of — see `docs/adr/0001` through `0005`.
- The pivot to "alongside, not intercepting" followed two RE sessions that confirmed OAE synthesizes crush-depth pressure as a *generic* impact fed through the engine's shared collision solver — there is no crush-depth-specific function to hook cleanly. The generic-solver findings are retained as closed context in `docs/re-notes/01-crush-depth-damage-signature.md`.
- Full context and terminology: `CONTEXT.md` at the repo root.
