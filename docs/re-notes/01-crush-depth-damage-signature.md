# RE note: crush-depth damage function — AOB signature

Target for issue `01-re-crush-depth-damage-signature`. Goal: a wildcarded AOB
signature that uniquely resolves the entry point of the function that applies
hull damage past crush depth, so ape.act's AOB scan can find it at runtime
(ADR-0003).

**OAE build:** `4B6BEE20` (see `oae-builds.md`)
**Verified on:** _TBD (date)_
**Tools:** Cheat Engine (dynamic discovery) + x32dbg (disassembly confirmation)

**Known hsie params (context):** hull `Hit_Points = 500.0` (float), past-crush-depth
`CrashSpeed = 7.0` damage/s (the single flat rate APE replaces). The target
function is the code that subtracts `CrashSpeed`-per-tick from the `Hit_Points`
float while the boat is below crush depth.

---

## ▶ NEXT SESSION — START HERE

The hull-write we traced leads only into the engine's **generic collision
system** (see §"CONCLUSION / STRATEGY PIVOT" below) — not a crush-depth-specific
function. Do **not** resume by climbing collision frames. Instead run the
**`CrashSpeed` data probe**:

1. In x32dbg, clear the breakpoint at `02B930D7` and let the game run.
2. Set `CrashSpeed` to a distinctive exact value (e.g. `43`, not a round number);
   reload; confirm crush-depth damage now ticks ~43/s (proves it's live).
3. Cheat Engine: Float, **Exact Value** `43`, First Scan. If >handful of hits,
   change `CrashSpeed` to another exact value (e.g. `37`), reload, Next Scan to
   isolate the one address that tracks it.
4. Right-click that address → **"Find out what ACCESSES this address"** (reads,
   not writes). Dive past crush depth so damage ticks.
5. The accessing instruction is **inside the real crush-depth damage function**
   (has depth + crush depth in scope). Capture its address + opcode +
   `module+offset`. That read-site is APE's hook target → then derive the AOB
   signature (§3) and verify uniqueness.

**Fallback** if the access list stays empty (CrashSpeed cached at load, not read
per-tick): find **current depth** or the sub's **crush-depth** value in memory
and trace *its* readers instead.

Environment is ready (user confirmed CE + x32dbg + OAE install all set up).

---

## 1. Discovery method

Approach: **hull-integrity value scan** — locate the boat's hull-health value in
memory while taking crush-depth damage, then "find what writes this address" to
reach the damage code.

### Scan log

- sh3.exe image base confirmed = `0x00400000` (from `sh3.exe+166A28` == abs
  `0x00566A28`). Image size 0x159000, so sh3.exe spans ~`0x00400000`–`0x00559xxx`.

- **Authoritative hull value** (float): per-object field at `[ebx+0xB8]`; observed
  live at heap addr `3D6E1290` (stale across reloads — heap object relocates).
  - Writer: `02B930D7 — D9 9B B8000000 — fstp dword ptr [ebx+0xB8]`.
  - `0x02B930D7` lives in **`sh3collisions.act`** — a stock SH3 collision/damage
    module (x32dbg reads `ebx` near a `"CRes"` string → collision-result object).
    This is a **third .act module** beyond hsie.act/sh5.act, and it is APE's AOB
    **scan target module** for this hook (ADR-0003). ape.act must resolve
    `sh3collisions.act`'s base at runtime and scan within it.
    - Module base: `0x02B90000`; `.text` at `0x02B91000` (size `0x18000`).
    - Store instruction module offset: **`+0x30D7`** (`02B930D7 − 02B90000`).
  - The store sits near the function **tail**: `… fstp [ebx+B8]; pop ebx;
    fstp st(0); fld [<data>]; add esp,1C; ret 8` → a `stdcall(2 args)` returning
    a float. **Entry point is above (TBD).** The damage math (`fld [ebx+B8]`,
    depth check, `fsub`) is between entry and this store.

- **Discarded: global HP mirror** at `sh3.exe+166A28` (`0x00566A28`).
  - Writer: `004E1C9F — A3 286A5600 — mov [00566A28],eax` (inside sh3.exe).
  - Rejected: write count keeps climbing every frame *above* crush depth while
    the value is static → per-frame "copy HP to global" refresh (HUD/damage
    screen), not the damage calc. Resets on reload (static global).

- **Open question blocking the signature:** which module owns `0x02B930D7`?
  Determines the AOB scan target module (ADR-0003). If it's a named module
  (hsie.act / sh3.dll) we signature within it; if it's a private VirtualAlloc'd
  code cave, approach changes (signature the sh3.exe call site into it instead).

- **Function containing the store is a GENERIC collision/impact-damage solver,
  not crush-depth-specific.** Disassembled `02B93035`–`02B930FB` (sh3collisions.act;
  entry is above 02B93035, uses ebp/esi/ebx, `ret 8` = stdcall/2 args, returns a
  float). Shape:
  - Dot-products a stack vector (`[esp+18/1C/20]`) with an object vector
    (`[eax+80/84/88]`, or `[ebp+90/94/98]` when `[ebx+C8]==0`), scaled by consts
    `[2BA9840]` (+0x19840), `[eax+C0]`, `[eax+C4]`, `[esp+2C]`.
  - `02B930B7 fcom [2BA9624]` (+0x19624, a **min-damage threshold**);
    below threshold → `jne 2B930F0` → exit without applying.
  - Apply path: `02B930C4 fld [ebx+B8]` → `fsub` → `02B930D7 fstp [ebx+B8]`
    (hull -= dmg), `mov [ebx+170],4` (damage/state flag). Two `ret 8` exits both
    `fld [2BA9624]` as the return value.
  - **Implication:** hooking here would affect *all* impact damage (gunfire,
    collisions, grounding), not just crush depth. **The crush-depth decision
    (depth vs crush depth, CrashSpeed rate) is in the CALLER.** Next: breakpoint
    the store, dive past crush depth, read the call stack to find the
    crush-depth-specific caller — that is the real hook target for this hack.

- **Call stack at the hull write** (Main Thread 9128, outer → inner), captured at
  the `02B930D7` breakpoint while below crush depth:
  ```
  004042C3  sh3.exe          main loop
  004C77E8  sh3.exe
  004CC764  sh3.exe
  004D7409  sh3.exe          sObject::Action(float)+50   (generic per-object tick, dt)
  1000E830  <module @ 0x10000000>                         object-specific behavior  <-- prime hook candidate
  02B93AE0  sh3collisions.act                             collision entry
  02B930D7  sh3collisions.act                             hull-damage solver (BP)
  ```
  - `sObject::Action(float)` (sh3.exe ~0x004D73B9) and the collision solver are
    both too generic to hook. The `0x10000000` module is **`kernel.dll`**, and
    the `1000E830` frame is a **generic virtual-dispatch loop** (call at
    `1000E82D = call [eax+24]`, vtable slot +0x24) — engine plumbing, no depth
    logic. It reveals that `02B93AE0` is the **+0x24 virtual method of the
    collision/damage component** (in sh3collisions.act). That component method —
    directly above the solver — assembles the impact/pressure force and is the
    next candidate for the depth-vs-crush-depth decision. Inspect `02B93AE0`.

- **`02B93AE0`'s function (entry `02B93A40`) is generic collision-pair
  resolution — NOT crush-depth logic.** It computes bounding volumes
  (`utl::ComputeBound…`), `rep movsd`-copies two 13-dword vectors (pos+vel of the
  two colliding objects) to locals, then calls the solver (`2B92E70`). No depth
  vs crush-depth compare anywhere. Confirms the whole kernel→collision subtree is
  the engine's generic collision system.
- **Solver function entry = `02B92E70`** (module offset `+0x2E70`); the store
  `02B930D7` is its tail.

- **CONCLUSION / STRATEGY PIVOT:** climbing collision frames is a dead end — the
  crush-depth decision (depth vs crush depth, `CrashSpeed` rate) is not in this
  chain. Two open possibilities: (1) crush-depth pressure is synthesized as a
  force upstream with `CrashSpeed` applied there; or (2) the traced hull writes
  were a real collision (e.g. seabed), i.e. the wrong mechanic. **Next approach:
  data-driven — exploit the configurable `CrashSpeed` as a unique marker: set it
  to a distinctive value, locate it in memory, then "find what ACCESSES this
  address" to land directly on the crush-depth damage code (where depth/crush
  depth live). That read-site is APE's real hook target.**

- Function entry point (real hook target): _TBD (via CrashSpeed access-trace)_

## 2. Confirmation (x32dbg)

- Function entry disassembly (first ~16–32 bytes): _TBD_
- Why this is the right function (evidence it's crush-depth-specific, not a
  generic damage/health writer): _TBD_

## 3. AOB signature

```
_TBD_        ; bytes with ?? wildcards
```

- **Wildcards & why:** _TBD (e.g. rel32 call operand at +N, absolute ptr at +M)_
- **Uniqueness check:** _TBD — CE AOB scan over sh3.exe returned exactly 1 match_
- **Offset from match to used entry point:** _TBD (usually 0)_

## 4. Volatile-byte notes

_Anything expected to shift on the next OAE rebuild; how to re-verify quickly._
