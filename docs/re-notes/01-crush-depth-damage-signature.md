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

**Status (2026-07-14):** the damage-*application* seam is fully confirmed as the
engine's generic collision solver (see "2026-07-14 confirmations" in §1). The
crush-depth *decision/generator* — the upstream code that has depth + crush depth
+ `CrashSpeed` in scope — is **still unlocated**. Run the **differential depth
access-trace** below to find it. Do **not** resume by climbing collision frames,
and do **not** use the old `CrashSpeed` access-probe (`CrashSpeed` is cached at
load and never re-read per tick — confirmed dead).

### Per-run re-anchor (do this FIRST, every session — heap + ASLR relocate)

Object pointers move every launch; **module offsets are stable**. `sh3.exe` is a
static image at `0x00400000`. `sh3collisions.act` relocates (base `0x02B90000` on
07-13, `0x02B40000` on 07-14) but our offsets are constant.

1. **Hull field**, via the static mirror as a permanent handle:
   - "Find what writes" the mirror `0x00566A28` (`sh3.exe+166A28`).
   - The only writer is the getter `004E1C99 mov eax,[ecx+0xB8]` /
     `004E1C9F mov [00566A28],eax` (copies player hull → HUD global).
   - At that instruction **ECX = live player-boat object base** (uniquely the
     player, since only the player hull is mirrored). Hull field = **ECX + 0xB8**.
   - ⚠ Read **ECX**, *not* EAX. EAX holds the HP *value* (a float, e.g.
     `0x43F3A666` ≈ 487); ECX is the *pointer*.
2. **Current depth**: Exact-Value float scan on the depth gauge → the field at
   **`[movementObj + 0x54]`** (§1, 07-14).

### The trace to run: differential depth access-trace → crush-depth value + generator

Rationale: crush depth is **compared against current depth every tick**, only
mattering when deep — so let the *code* isolate it (scanning for the value
`≈ 221` returns too many static candidates to pick from).

1. Right-click the **current-depth** address → **Find out what ACCESSES this**.
2. **Above** crush depth (no damage): note the baseline readers (physics, gauge,
   the `0x00514000` clamp code).
3. **Dive below** crush depth (hull bleeding): watch for a **new** reader, or one
   that only starts climbing now. That instruction loads current depth to compare
   against crush depth = the **crush-depth damage check**.
4. Break there. The **other operand** it compares depth against **is the
   crush-depth value** (isolates it from the 221-candidates). Its surrounding code
   + call stack = the **impact generator** — the clean, sub-specific hook target.
5. **Fallback**: if no new reader appears on the gauge-depth field when deep, the
   generator reads a different depth representation — run the same differential on
   the **world-space Z** at the `[obj+0x10]` field (candidate B, §1, 07-14).

Environment is ready (CE + x32dbg + OAE install all confirmed present).

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

- Function entry point (real hook target): **still TBD** — pursuit is now the
  crush-depth *generator* via the differential depth access-trace (see START
  HERE), not the CrashSpeed probe.

### 2026-07-14 session — confirmations (build 4B6BEE20)

Re-ran the hull-write trace **with a depth-toggle control**, so the break is now
*proven* to coincide with pressure-only damage (deep calm water, no combat;
`CrashSpeed` lowered to `0.03` for a long, survivable bleed).

- **Damage-application seam = the generic collision solver, re-confirmed.** Same
  writer as 07-13, re-derived cleanly: `02B430D7 fstp [ebx+0xB8]` at module base
  `0x02B40000` → **offset +0x30D7**; solver entry **+0x2E70**. Surrounding bytes
  unchanged (`fsub; mov [ebx+170],4; fstp [ebx+B8]`). `ebx` = the collision/damage
  object (hull at +0xB8), **not** the sub's spatial object.
- **Call stack during a *proven* pressure tick is identical to 07-13 — all
  generic:**
  ```
  004042C3 (main loop) → 004CC7E8 → 0044C764
    → 004D7409  sObject::Action(float)+50
    → 1000E830  kernel.dll  (generic vdispatch, call [eax+24])
    → 02B43AE0  sh3collisions collision entry (+0x3AE0)
    → 02B430D7  sh3collisions store            (+0x30D7)
  ```
  ⇒ **Crush-depth pressure is synthesized as a generic impact and pushed through
  the same solver as gunfire/ramming/grounding.** No crush-depth-specific frame is
  on the damage stack; the depth/CrashSpeed decision runs *upstream* as the impact
  **generator** (not a caller here). 07-13's stack was NOT a wrong-mechanic fluke —
  this really is the pressure path.
- **Pressure magnitude:** at `CrashSpeed=0.03`, per-tick damage = `ST(1) ≈ 0.00265`
  HP (at the store, `ST(0)` = new hull, `ST(1)` = amount subtracted). Verified the
  hull field bled `495 → 483` while held below crush depth.

**Depth values located (on objects *other* than the collision object):**
- **Current/gauge depth = `[movementObj + 0x54]`** — written by depth-clamp code
  `00514159/0051416B`, compared at `00514001`. Movement-obj base e.g. `3CB236E0`.
- **World-space Z (candidate B)** = a field read as `[obj+0x10]`/`[ecx]` from many
  physics/render sites (7740× from a `0x02BFxxxx` module) — fallback depth source.
- **`0x00514000` is depth *movement/rate-clamp*, NOT crush depth** — ruled out. It
  writes commanded depth `X=[esp+14]` into `[ecx+0x54]`/`[ecx+0x58]` and clamps the
  per-tick change against `[0x00520640] = 3.0` (max ~3 m/s). The `ST(0)=222.87` we
  saw there was commanded/new depth.
- **Crush depth ≈ 221 m** (±0.5) by damage-onset; too many static value-candidates
  to isolate by value → use the differential access-trace (START HERE).

**Two viable hook architectures (decision pending the generator hunt):**
- **(A) Hook the generic solver** (`+0x2E70`/`+0x30D7`, stable offsets) and
  discriminate at runtime — is `ebx` the player sub? below crush depth?
  pressure-sized magnitude? — then overwrite `ST(1)`/the stored value with APE's
  banded damage. Downside: reachability (depth & crush depth live on *other*
  objects; combat-vs-pressure discrimination is heuristic).
- **(B) Hook the crush-depth generator** (current pursuit): cleaner — depth, crush
  depth, and the damage are all in scope, and it's sub-specific by construction.
  Found via the differential depth access-trace.
  → **Recommendation: pursue (B); fall back to (A) if no clean generator seam
  exists.**

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
