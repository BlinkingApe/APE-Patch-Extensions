# Live-game session playbook — issue 01 dynamic half

**Purpose.** A step-by-step for the *at-the-controls* Cheat Engine session that
finishes issue 01. The static half is done (see
`01-crush-depth-damage-signature.md` §1/§4); this doc is the ordered checklist for
the parts that need SH3 actually running with a human diving the boat.

**Build under test:** OAE `4B6BEE20` at
`C:\games\OneAlex Extended 1.5.2_pre-release\sh3.exe`. If the SHA-256 no longer
matches, the anchors below may have moved — re-run `tools/re-static/sh3re.py` first.

**Tools:** Cheat Engine (dynamic discovery), Binary Ninja GUI (read the functions
CE traces land in — no headless API here), the game itself.

**Already in hand (from the static pass):**
- Hull HP field `[boat+0xB8]`; AOB `8B 81 B8 00 00 00 A3 28 6A 56 00` @`004E1C99`.
- **Static HP-value mirror `[0x00566A28]`** — a fixed address holding the player's
  current hull HP as a float, refreshed every frame. Read-only for our purposes
  (the engine overwrites writes), but a zero-effort live readout for this session.
- Current depth field `[movementObj+0x54]`; AOB
  `D9 44 24 14 D8 59 54 DF E0 F6 C4 41 0F 85 ?? ?? ?? ??` @`00513FFD`.
- Candidate static chain roots: `0x005449B0`, `0x0055408C`, `0x005543DC`
  (`[[0x5449B0]+0xD8]` is a manager/"god" object, NOT the player — don't expect the
  player fields directly off it).

## Setup (do once)

1. Launch OAE, load a **single-player patrol** with a submerged-capable boat (a
   Type VII is fine). Pick **calm water, no contacts**, so nothing but pressure
   touches the hull.
2. Attach Cheat Engine to `sh3.exe`. Confirm the image base is `0x00400000`
   (non-ASLR) so static addresses line up with the notes.
3. Sanity-check the HP mirror: add address `0x00566A28`, type **Float**. It should
   read your current hull integrity and tick as you take damage. This is your
   live HP oracle for the whole session.
4. **Temporarily** set the test boat's `CrashSpeed` to a small non-zero value (e.g.
   `0.05`) in its data file so you get a slow, controllable pressure bleed to trace
   against. (Production ships `CrashSpeed=0`; this is only to make the trace
   observable. Revert after.)

## Order of work (easiest first)

### 1. Mission clock (#4) — do first, it's the cleanest

1. CE **Value type: Float** (also try Double/4-byte if float fails), scan while the
   game runs: **Unknown initial value → Increased value** a few times, sitting still.
2. Confirm the survivor(s): **pause the game** → the value should stop
   (Unchanged); **raise time compression** → it should climb faster. That behavior
   uniquely identifies the mission clock.
3. Record: address, **format/resolution** (float seconds? a date-time struct? note
   the unit and epoch), and a pointer chain via "find what accesses" → prefer a path
   rooted at a static (`0x5449B0` etc.). Write it into `01-...-signature.md` §1 #4.
   - The loop only needs `dt = Δ(clock)` in game-seconds, so the *unit* matters most.

### 2. Current depth → `movementObj` chain (#2)

1. CE **Float** exact-value scan on the depth gauge reading; change depth; next
   scan; repeat until you have the field. Cross-check: it should equal
   `[movementObj+0x54]`.
2. Right-click → **Find out what accesses this address**. Dive/hold depth; the
   readers include the depth code around `00513FC0`/`00514180`. Read the base
   register that holds `movementObj`.
3. Recover a `[static + off + …]` chain to `movementObj` (pointer-scan, or walk the
   base register back). **Re-verify it survives a relaunch.** Record in §1 #2.

### 3. Player boat → hull HP chain (#1, for the *write*)

1. Start from the HP mirror `0x00566A28`: **find out what writes** it → lands on the
   getter store `004E1C9F`; at `004E1C99` **ECX = the player boat base** (read ECX,
   not EAX). That gives you the live `boat` address.
2. From that live `boat`, **pointer-scan** to a static-rooted chain (again prefer
   `0x5449B0`/`0x55408C`/`0x5543DC`). Confirm `[boat+0xB8]` == the HP mirror value.
   Re-verify across a relaunch. Record in §1 #1.

### 4. CDT / crush depth (#3) — the differential trace

1. On the current-depth address (#2), **find out what ACCESSES** it. Above crush
   depth, note the baseline readers.
2. **Dive below crush depth** (≈221 m; use the temp `CrashSpeed` bleed to know you
   crossed). Watch for a **new** reader that only fires when deep — that instruction
   compares depth against CDT.
3. Break there; the **other operand** of the compare **is the CDT value**. Record
   its address + offset chain (read live — mods randomize CDT, never hardcode).
   Fallback: if no new reader appears on `+0x54`, run the same trace on world-space
   Z `[obj+0x10]`. Record in §1 #3.

### 5. `CrashSpeed` data file (#D) — non-debugger

Find which OAE data file holds per-unit `CrashSpeed` for each **playable** u-boat
class (`.sim`/`.cfg` under the boat's data folder). List the files/paths so issue 07
can author the `CrashSpeed=0` edits. Then **revert** your temp `CrashSpeed` change
from setup step 4.

## Integration (back at the keyboard, no game needed)

The loop and its seam are already built and fail inert. To wire the results:

1. Add `src/mods/crush_depth_damage/live_game_state.cpp` implementing
   `ape::IGameState` (`src/mods/crush_depth_damage/game_state.hpp`):
   `resolve()` runs the AOB scans (signatures above) + roots the chains
   recorded in §1; `sample()` reads depth/CDT/HP/clock;
   `write_hull_hp()` writes `[boat+0xB8]`. Keep the plausibility ranges in
   `LoopLimits` honest to the real magnitudes you observed (esp. depth/CDT units and
   the clock's `dt` scale).
2. In `dllmain.cpp`, swap `UnresolvedGameState` for the new class. Add the new
   source to `ape_act` in `CMakeLists.txt`.
3. **Verify (issue 05 acceptance):** inject `ape.act` via x32dbg into the running
   game (no patcher needed yet), force the boat to each band's test depth in CE, and
   check `<game root>\ape_act.log`: the `[dmg]` lines' arithmetic
   (`rate·dt`, %-under, band) must match; pausing halts HP loss; time compression
   scales it. A missed scan must log `[inert]` and apply nothing.

## Where things plug in

- Record raw findings in `01-crush-depth-damage-signature.md` §1 (per-target) and
  update the §-START-HERE status table.
- Update issue `01` (`.scratch/crush-depth-damage/issues/01-...md`): check the boxes
  as each target lands; flip Status off `needs-info` once all four + CrashSpeed are
  recorded.
- Then issue `05` is unblocked to completion; issue `02` (load-site RE) → `06`
  (patcher) → `07` (packaging, incl. the CrashSpeed edits) remain.
