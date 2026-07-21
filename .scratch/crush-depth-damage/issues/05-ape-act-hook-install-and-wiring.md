# ape.act damage-loop install, live wiring, and debug logging

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

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
- 03 (damage-band decision function — pivot rework)
- 04 (config loading — pivot rework)
