# RE note: crush-depth damage — live-value anchors (post-pivot)

Target for issue `01`. **Architecture pivot (2026-07-21):** APE no longer hooks a
crush-depth damage *function*. It runs its own loop in ape.act and applies banded
damage by reading/writing memory values, never touching a stock SH3 function
(PRD; ADR-0001). So this note's goal changed: instead of *one* AOB signature for a
damage function, we now need **AOB-anchored pointer chains for four live values**,
each recorded per OAE build (ADR-0003). The old "find the damage function / its
generator" hunt is **closed** — see §3 for why (retained as context).

**OAE build:** `4B6BEE20` (see `oae-builds.md`)
**Verified on:** _TBD (date)_
**Tools:** Cheat Engine (dynamic discovery) + x32dbg (disassembly confirmation)

---

## ▶ NEXT SESSION — START HERE

We need runtime-resolvable, AOB-anchored handles for these, all rooted in **stable
stock sh3.exe** (static image at `0x00400000`, never recompiled; only per-launch
ASLR/heap moves the live objects):

| # | Value | Offset / chain | Status |
|---|-------|----------------|--------|
| 1 | **Player boat object → hull HP** | HP = `[boat + 0xB8]` | offset known; need AOB anchor + chain to `boat` |
| 2 | **Current depth** | `[movementObj + 0x54]` | offset known; need AOB anchor + chain to `movementObj` |
| 3 | **CDT (crush depth)** | TBD | **location unknown — main hunt** |
| 4 | **Mission clock** | TBD | **new hunt** (drives game-second `dt`; see PRD timing) |

Plus one **non-memory** task: find which OAE **data file** holds the per-unit
`CrashSpeed` so we can author the `CrashSpeed`=0 edit for each playable u-boat class
(neutralization; PRD). This is a `.sim`/`.cfg` data-file hunt, not a debugger task.

### Per-run re-anchor (do this FIRST, every session — heap + ASLR relocate)

Object pointers move every launch; **module offsets are stable**. sh3.exe is a
static image at `0x00400000`.

1. **Hull field**, via the static mirror as a permanent handle:
   - "Find what writes" the mirror `0x00566A28` (`sh3.exe+166A28`).
   - The only writer is the getter `004E1C99 mov eax,[ecx+0xB8]` /
     `004E1C9F mov [00566A28],eax` (copies player hull → HUD global).
   - At that instruction **ECX = live player-boat object base** (uniquely the
     player, since only the player hull is mirrored). Hull field = **ECX + 0xB8**.
   - ⚠ Read **ECX**, *not* EAX. EAX holds the HP *value* (float, e.g. `0x43F3A666`
     ≈ 487); ECX is the *pointer*.
   - **For runtime resolution (target #1):** signature the getter site around
     `004E1C99`, and trace where ECX is loaded from just before — that source is
     the static→chain path ape.act follows to reach `boat` without a debugger.
2. **Current depth (target #2):** Exact-Value float scan on the depth gauge → the
   field at **`[movementObj + 0x54]`**. Then signature a site that references
   `movementObj` to derive the chain.

### Target #3 — locate the CDT *value* (differential depth access-trace)

We only need CDT's **address/offset now**, not a "generator" function. Rationale:
crush depth is compared against current depth every tick, mattering only when deep,
so let the *code* isolate it (scanning for `≈ 221` returns too many static
candidates).

1. Right-click the **current-depth** address → **Find out what ACCESSES this**.
2. **Above** crush depth (no damage): note baseline readers (physics, gauge, the
   `0x00514000` depth-rate clamp).
3. **Dive below** crush depth: watch for a **new** reader (or one that only starts
   climbing now) — that instruction loads current depth to compare against crush
   depth.
4. Break there. The **other operand** it compares against **is the CDT value** —
   record its address and, via the instruction, its offset/chain so ape.act can
   read it live (mods randomize CDT, so it must be read, never hardcoded).
5. **Fallback:** if no new reader appears on the gauge-depth field when deep, the
   compare uses a different depth representation — run the same differential on the
   **world-space Z** at `[obj + 0x10]` (candidate B, §2).

### Target #4 — locate the mission clock

Find the in-game mission time (the value that advances with time compression and
freezes on pause). Easy to isolate: it's monotonically increasing and human-
readable — Exact/Increased-Value scans while the game runs, then confirm it
freezes on pause and accelerates under time compression. Record its address +
chain and its **format/resolution** (float seconds vs. a date-time struct), since
the loop computes `dt = Δ(clock)` in game-seconds.

Environment is ready (CE + x32dbg + OAE install all confirmed present).

---

## 1. Live-value anchors (fill in as resolved)

For each: AOB signature (wildcarded), the static operand extracted, the offset
chain to the value, uniqueness check (exactly 1 match over sh3.exe), and any
volatile-byte notes.

- **#1 Player boat / hull HP:** _TBD_
- **#2 Current depth:** _TBD_
- **#3 CDT:** _TBD_
- **#4 Mission clock:** _TBD_

## 2. Value locations found so far (heap addrs are per-launch; offsets are stable)

- **Hull HP** (float): per-object field `[boat + 0xB8]`. Static mirror handle:
  `0x00566A28`, writer/getter `004E1C99`/`004E1C9F` (ECX = player boat).
- **Current/gauge depth = `[movementObj + 0x54]`** — written by depth-clamp code
  `00514159`/`0051416B`, compared at `00514001`. (`0x00514000` is depth
  *movement/rate-clamp*, NOT crush depth — ruled out; its `[0x00520640]=3.0` is the
  max depth rate.)
- **World-space Z (candidate B)** = a field read as `[obj + 0x10]`/`[ecx]` from many
  physics/render sites — fallback depth source for the target-#3 trace.
- **Crush depth ≈ 221 m** (±0.5) by damage-onset; too many static value-candidates
  to isolate by value → use the differential access-trace above.

## 3. CLOSED context — why we don't hook a damage function

Retained so a future session doesn't re-walk this dead end. Two sessions (07-13,
07-14, build `4B6BEE20`) confirmed:

- **The hull-damage writer is the engine's GENERIC collision solver**, not
  crush-depth-specific: store `fstp [ebx+0xB8]` at `sh3collisions.act` offset
  **+0x30D7**, solver entry **+0x2E70** (module base relocates per launch via
  ASLR). The same solver applies gunfire, ramming, and grounding damage.
- **Crush-depth pressure is synthesized as a generic impact** and pushed through
  that solver. With a depth-toggle control (deep calm water, no combat,
  `CrashSpeed` lowered to `0.03` for a slow bleed), the break was *proven* to
  coincide with pressure-only damage, and the call stack was entirely generic:
  ```
  main loop → sObject::Action(float) → kernel.dll vdispatch (call [eax+24])
    → sh3collisions collision entry (+0x3AE0) → solver store (+0x30D7)
  ```
  No crush-depth-specific frame is on the damage stack; `ebx` there is the
  collision/damage object (hull at +0xB8), not the sub's spatial object.
- **The upstream "generator"** (the code that decides depth-vs-crush-depth and
  feeds `CrashSpeed` in) was never located, and hooking the generic solver would
  affect *all* impact damage. Per-tick magnitude at `CrashSpeed=0.03` was
  `≈ 0.00265` HP (`ST(1)` at the store).
- `CrashSpeed` is **cached at load** — never re-read per tick — so the old
  "CrashSpeed access-probe" is also a dead end for finding live logic.

**Conclusion → pivot:** there is no clean crush-depth-specific function to hook.
APE instead runs alongside and writes HP directly, and neutralizes stock by setting
per-unit `CrashSpeed`=0 in data (so the generic-solver pressure path contributes
nothing). This note's live targets (§ START HERE) replace the abandoned
function/generator hunt.
