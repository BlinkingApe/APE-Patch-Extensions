# ape.act damage-loop install, live wiring, and debug logging

Status: needs-info (superseded by the tier-model pivot — see amendment below; not touched by ticket 13, discovered stale while auditing the map)

## ⚠ SUPERSEDED BY THE TIER-MODEL PIVOT (amended 2026-08-17)

Everything below this point describes the **abandoned three-band, fraction-of-crush-depth
model** (CDT reads, `select_band`, a single `[CrushDepthDamage]` config section, one
band-config struct). The map (`.scratch/depth-pressure-stress/map.md`) superseded that
whole model; ticket 13 deleted the sibling tickets `03`/`04`/`06`/`07` that described its
pure function and config loader, but this ticket — the actual end-to-end wiring — wasn't
on its list. It should have been: it's the same staleness, just further along, with real
code already built and still linked into the build (`src/mods/crush_depth_damage/`,
`CMakeLists.txt`, `tools/config_check/main.cpp`, `tests/damage_band_test.cpp`).

**What's still true and reusable:** the *shape* of the offline scaffold — `DllMain`
spawning a background loop thread with no function hooks (ADR-0001), a resolve/sample
seam (`IGameState`) that fails inert until real anchors are wired in, plausibility gates
against insane reads, a discontinuity guard against save-load jumps, and a thread-safe
append logger. None of that is model-specific; the new tier model needs the same
skeleton.

**What's dead:** the *effect* logic — `select_band`, CDT resolution, the
`[CrushDepthDamage]` band-config struct, and the "subtract rate·dt from
`[boat+0xB8]`" write shape (the new model is poll-and-amplify — notice HP fell by N,
subtract a further `(mult−1)·N` — not "compute a rate and subtract it").

**Not resolved here.** Rewiring this scaffold onto the tier model is real implementation
work that depends on tickets `08`/`09`/`10`/`11` (the live-value hunts) and `12` (the tuning
numbers) resolving first, and on the PRD rewrite the map's destination calls for — the map
sequences RE before implementation. This ticket is left **needs-info**, not resolved or
deleted, as a placeholder for that rewiring; a fresh ticket should probably replace it
once the PRD is rewritten, rather than reopening this one's now-wrong acceptance criteria.
The dead source it references (`src/mods/crush_depth_damage/*`, its CMake target, its
`config_check` tool, its test) is tracked for removal in ticket `15`.

---

## Original ticket (2026-07-21, now superseded — kept for history)

Status: ready-for-agent (offline scaffold DONE; remainder blocked on issue 01 — see Comments)

## Parent

.scratch/depth-pressure-stress/PRD.md

## What to build

The core end-to-end slice, post-pivot. `ape.act`'s `DllMain` spawns a **background
loop thread** on attach (and signals it to stop on detach) — **no MinHook, no
function hooks** (ADR-0001). The thread:

1. Loads the band config via `load_band_config` (issue 04), resolving `ape.cfg`'s
   path relative to the game root.
2. AOB-resolves the live-value anchors from issue 01 (player boat → hull HP,
   current depth, CDT, mission clock), following the recorded pointer chains.
3. On each iteration: reads current depth, CDT, hull HP, and the mission clock;
   computes `dt = Δ(mission clock)` in game-seconds; if the boat is below crush
   depth, calls `ape::select_band` (issue 03) and subtracts `rate · dt` from hull
   HP by writing `[boat + 0xB8]` directly (plain read-modify-write, HP read fresh
   each iteration).
4. **Fails inert:** if any required value can't be resolved or fails a plausibility
   gate, the iteration applies no damage and logs the reason. Never guesses.
5. Logs depth, percent-under-crush-depth, selected band, applied rate, `dt`, and HP
   applied on every iteration that applies damage.

AOB scanning and memory access run on the thread, never under the DLL loader lock.

## Acceptance criteria

- [ ] `ape.act` builds as a PE32 DLL via CMake/MSVC (MinHook no longer linked).
- [ ] `DllMain` spawns the loop thread on attach and cleanly signals it to stop on
      detach.
- [ ] On the thread, the four live-value anchors resolve via AOB scan against the
      real OAE sh3.exe; a missed scan fails inert (logged), does not crash or apply
      damage.
- [ ] Live depth, CDT, hull HP, and mission clock are read each iteration; `dt` is
      computed from the mission clock (verified 0 while paused, larger under time
      compression).
- [ ] `select_band` is called with correct inputs and its rate is applied as
      `hp −= rate · dt` written to `[boat + 0xB8]`; the open-ended heavy band keeps
      applying far below crush depth.
- [ ] Plausibility gates reject insane reads (HP, depth, CDT out of sane ranges) →
      fail inert.
- [ ] Every iteration that applies damage writes a debug log line with depth,
      percent-under, band, rate, `dt`, and HP applied.
- [ ] Verified via the debug-log + Cheat Engine-forced-depth loop: forcing the
      submarine to each band's test depths produces log lines matching expected
      arithmetic; pausing halts HP loss; time compression scales it in game-time.
- [ ] For development, `ape.act` may be injected into the running game via x32dbg
      rather than the production load-site patch — this ticket does not require
      02-re-sh3exe-load-call-site or the patcher tool.

## Blocked by

- 01 (live-value anchors: player boat/HP, current depth, CDT, mission clock)
  — **only the real resolver + live verification remain blocked**
- 03 (damage-band decision function — pivot rework) — ✅ DONE
- 04 (config loading — pivot rework) — ✅ DONE

## Comments

**OFFLINE SCAFFOLD COMPLETE (2026-07-21, Windows 11).** Everything in this ticket
that does not require the live game is built and compiling as PE32/x86; only issue
01's resolver and the in-game verification remain.

Delivered:
- **`src/ape_act/game_state.hpp`** — the `IGameState` seam (`resolve` / `resolved`
  / `sample` → `Readings` / `write_hull_hp`) plus `UnresolvedGameState`, a stub that
  never resolves so the loop **fails inert** until issue 01 wires the real
  AOB-anchored resolver behind this interface. This is the single insertion point
  for issue 01 — the loop needs no further change.
- **`src/ape_act/damage_loop.{hpp,cpp}`** — the loop: resolve-with-retry, sample,
  plausibility gates (`LoopLimits`: HP/depth/CDT ranges + `dt_max_step`), `dt =
  Δ(mission clock)` with pause (`dt==0` → no damage), discontinuity guard
  (`dt<0` or `>dt_max_step` → skip + re-baseline, so a save-load jump can't one-shot
  the hull), applies `hp -= select_band(...).rate * dt` (writing `[boat+0xB8]` via
  the seam) only at/below crush depth, clamps HP at 0, and logs every
  damage-applying iteration (depth, %-under, band, rate, dt, hp before→after).
  Fail-inert lines are de-duped.
- **`src/ape_act/dllmain.cpp`** — rewritten for the pivot: spawns the loop thread on
  attach, signals+joins on detach, no work under the loader lock. Loads `ape.cfg`
  from `<game root>\data\Cfg\ape.cfg`; logs to `<game root>\ape_act.log`.
- **`src/ape_act/log.hpp`** — tiny thread-safe append logger (no-op if the file
  can't be opened; logging must never take the game down).
- **CMake**: `ape_act` no longer links MinHook and the `third_party/minhook`
  subdirectory add was removed (criterion "MinHook no longer linked" ✅). Verified
  `ape.act` still builds as **PE32 (machine 0x014C, magic 0x010B)**; `ape_tests`
  green.

Note on the test seam: I kept to the PRD's "exactly one automated-test seam"
decision — the loop is orchestration and is manually verified. The gate/dt logic is
factored into small pure helpers (`gate_reason`, the dt block) so it *could* be
promoted to a tested seam later; flagging that as a decision for the human, not
taken unilaterally.

Still blocked on issue 01 (all need the live game):
- [ ] Replace `UnresolvedGameState` with the real AOB-anchored resolver.
- [ ] Verify anchors resolve / miss-fails-inert against real OAE sh3.exe.
- [ ] Verify dt (0 paused, scales with time compression), band arithmetic in the
      log, HP write at `[boat+0xB8]`, plausibility rejections — via the debug-log +
      CE-forced-depth loop.
