# RE note: crush-depth damage — live-value anchors (post-pivot)

Target for issue `01`. **Architecture pivot (2026-07-21):** APE no longer hooks a
crush-depth damage *function*. It runs its own loop in ape.act and applies banded
damage by reading/writing memory values, never touching a stock SH3 function
(PRD; ADR-0001). So this note's goal changed: instead of *one* AOB signature for a
damage function, we now need **AOB-anchored pointer chains for four live values**,
each recorded per OAE build (ADR-0003). The old "find the damage function / its
generator" hunt is **closed** — see §3 for why (retained as context).

**OAE build:** `4B6BEE20` (see `oae-builds.md`; SHA-256 re-confirmed against the
`C:\games\OneAlex Extended 1.5.2_pre-release\sh3.exe` install on 2026-07-21)
**Static confirmation done:** 2026-07-21 (code anchors + AOB signatures)
**Dynamic discovery:** _still pending_ (player pointer chains, CDT, mission clock — see §4)
**Tools:** Cheat Engine (dynamic discovery) + static disassembly. Binary Ninja is
GUI/personal here (no headless API), so the static pass was scripted with Python
`pefile` + `capstone` (`scratchpad/*.py`, method in §5); use the BN GUI for
interactive cross-referencing when walking the chains found dynamically.

---

## ▶ NEXT SESSION — START HERE

⚠ **Superseded table (kept for history):** the 4-row table below predates the
tier-model pivot recorded in `.scratch/depth-pressure-stress/map.md`. CDT is now
**dead** (the new model never reads crush depth); astern seepage and the
electric-engine damage float were added as live-value hunts. The current, complete
worklist for **depth-pressure-stress** is:

| # | Value | Offset / chain | Status |
|---|-------|----------------|--------|
| 1 | **Player boat object → hull HP** | HP = `[boat + 0xB8]` | **code anchor + AOB DONE** (§1); HP-value *read* also available with **no chain** via static mirror `[0x566A28]`. Boat *pointer* (for writes) needs the dynamic chain-walk (§4) — **candidate static root found 2026-08-17: `0x5543DC`** (§6). |
| 2 | **Current depth** | `[movementObj + 0x54]` | **code anchor + AOB DONE** (§1); `movementObj` pointer needs the dynamic chain-walk (§4) — **same `0x5543DC` candidate is directly used in this function's guard code** (§6). |
| 3 | ~~CDT (crush depth)~~ | — | **dead.** Tier model never reads crush depth (map, Out of scope). |
| 4 | **Mission clock** | TBD | **dynamic hunt (§4)** (drives game-second `dt`) — no static candidate found yet (§6). |
| 5 | **Astern seepage / flooding trigger** | TBD | **dynamic hunt.** No static candidate found yet — see issue `10`; SH3's flooding model itself is unmapped. |
| 6 | **Electric-engine damage float** | `[[0x5543DC]+4]+0x??` (exact offset TBD) | **dynamic hunt, but strongly narrowed by §6** — same object and same array region as the diesel pair at `+0x30C`; a handful of neighbouring offsets are the short list to try first in CE (issue `11`). |

**Static pass complete (2026-07-21), extended (2026-08-17).** The original two code
anchors (§1) are located with a verified-unique AOB signature. A follow-up static
pass (§6) surveyed the `0x5543DC` singleton across both `.text` and h.sie's `.pcode`
region and found it directly implicated in **both** #1 and #2's code, plus the
diesel-damage cave (#6's neighbour) and the u-tanker hull-repair cave — making it
the single most promising static root to test first in CE, ahead of a blind
pointer-scan. Mission clock (#4) and the flooding trigger (#5) still have **no**
static candidate; they remain open-ended dynamic hunts. The AOB signatures are the
self-verification handles ADR-0003 calls for — once CE gives a static→object chain,
the matching AOB confirms the field offset (`+0xB8` / `+0x54`) is still valid for
this build.

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

### #1 Player boat / hull HP — CODE ANCHOR DONE

The hull-HP getter/HUD-mirror store (stock code; ECX = the object being updated,
which is the player when the mirror is refreshed):

```
004E1C99  8B 81 B8 00 00 00   mov eax,[ecx+0xB8]      ; ecx = boat, +0xB8 = hull HP (float)
004E1C9F  A3 28 6A 56 00      mov [0x566A28],eax      ; store into static HP mirror
```

- **AOB (literal, 11 bytes):** `8B 81 B8 00 00 00 A3 28 6A 56 00`
- **Uniqueness:** exactly **1** match across `.text`. ✅
- **No wildcards needed:** the offset `0xB8` and the absolute mirror address
  `0x00566A28` are both stable static values (image is non-ASLR, never recompiled).
- **Static operands extracted:** hull-field offset `+0xB8`; HP-value mirror
  `0x00566A28`.
- **HP mirror `0x00566A28` (float):** written here every frame, read at `004FC27E`
  (`mov eax,[0x566A28]`). It is a **read-only copy of the player's hull HP at a
  fixed address** — APE can read player HP with *no pointer chain*. (Writing it does
  nothing useful: the engine overwrites it from `[boat+0xB8]` each frame. Use it for
  the plausibility-gated read / "am I the player, am I damaged" check; the damage
  *write* still targets `[boat+0xB8]`.)
- **Getter function** `004E1C00`, **single call site** `004F9559`, where
  `ecx = ebp = [esp+0x1C]` — i.e. the object currently being updated, **not** a
  static-rooted pointer. The boat pointer for the *write* must be rooted dynamically
  (§4-A).

### #2 Current depth — CODE ANCHOR DONE

The depth setter and the depth compare (both on the movement object; ECX =
`movementObj`):

```
00513FE9  8B 54 24 14   mov edx,[esp+0x14]
00513FED  8B C2         mov eax,edx
00513FEF  89 51 54      mov [ecx+0x54],edx     ; +0x54 = current depth (float)
00513FF2  89 41 58      mov [ecx+0x58],eax     ; +0x58 = paired/ordered depth
...
00513FFD  D9 44 24 14   fld  [esp+0x14]
00514001  D8 59 54      fcomp [ecx+0x54]       ; compare against current depth
00514004  DF E0         fnstsw ax
00514006  F6 C4 41      test ah,0x41
00514009  0F 85 41 01 00 00  jne 0x514150
```

- **AOB anchor (depth-compare site, jne rel32 wildcarded):**
  `D9 44 24 14 D8 59 54 DF E0 F6 C4 41 0F 85 ?? ?? ?? ??`
- **Uniqueness:** exactly **1** match across `.text`. ✅ (The bare setter
  `89 41 54` = `mov [ecx+0x54],eax` alone occurs 8×, too common to anchor on.)
- **Static operand extracted:** current-depth field offset `+0x54` (float);
  `+0x58` is a paired depth slot set alongside it.
- **Setter function** `00513FC0`, **single call site** `0051457A`
  (`ecx = esi = this` of the method at ~`00514560`) — again a `this`, so
  `movementObj` must be rooted dynamically (§4-A).

### #3 CDT — pending (dynamic, §4-B)
### #4 Mission clock — pending (dynamic, §4-C)

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
- **Static singleton roots (candidate chain anchors, from the 2026-07-21 pass):**
  - `0x005449B0` — a heavily-used static singleton **pointer** (`mov reg,[0x5449B0]`
    appears 823×). `[[0x5449B0]+0xD8]` is a large **manager/"god" object** (offset
    `+0x83C` dereferenced 641×, `+0x7EC` 45×, …) — it is **not** the player movement
    object, so don't expect `+0x54`/`+0xB8` directly off it. It's the likely root to
    *start* a dynamic walk from, but the exact offset path to the player is a
    haystack statically.
  - `0x0055408C`, `0x005543DC` — other static singletons used as `this`/guards in the
    depth-handling function region (`00514180`+, `00514560`+). Worth testing as
    alternative chain roots in CE.

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

## 4. Remaining DYNAMIC worklist (needs the live game + a human)

Static analysis has taken §1 as far as it goes: the code anchors and their AOB
signatures are fixed. Everything below needs Cheat Engine on the running game
(dive the boat, watch/trace live). The BN GUI is useful here for reading the
functions the CE traces land in.

**§4-A — root the two player pointer chains.** For each of HP (`[boat+0xB8]`) and
depth (`[movementObj+0x54]`):
1. In CE, find the live field: HP via the static mirror `[0x566A28]` (read it, then
   "find what writes"/"find what accesses" to reach `boat`); depth via an
   Exact-Value float scan on the gauge → `[movementObj+0x54]`.
2. Right-click the live address → **Pointer scan** (or "find what accesses" and read
   the base register) to recover a `[static + off + off …]` path. Prefer paths
   rooted at the singletons in §2 (`0x5449B0` / `0x55408C` / `0x5543DC`); re-verify
   the path survives a relaunch.
3. Record the full chain. The AOB signatures in §1 then serve as the build-check:
   they confirm the field offsets (`+0xB8`, `+0x54`) are unchanged for this build.

**§4-B — CDT (crush depth).** Differential depth **access-trace** (unchanged from
the recipe above): "find what accesses" the current-depth field; note baseline
readers above crush depth; dive **below** crush depth and watch for a *new* reader —
that instruction compares depth against CDT, and its other operand **is** the CDT
value. Record CDT's address + offset chain. Read live (mods randomize it), never
hardcode. Fallback: run the same trace on world-space Z `[obj+0x10]` if no new
reader appears on `+0x54`.

**§4-C — mission clock.** Exact/Increased-Value scans while running; confirm it
freezes on pause and accelerates under time compression. Record address, chain, and
**format/resolution** (float seconds vs. date-time struct) — the loop needs
`dt = Δ(clock)` in game-seconds.

**§4-D — `CrashSpeed` data file (non-debugger).** Identify which OAE data file
(`.sim`/`.cfg`/per-unit) holds `CrashSpeed` for each playable u-boat class, for the
`CrashSpeed`=0 neutralization edits (hand-off to issue 07).

## 5. Static-analysis method (reproducible)

The 2026-07-21 static pass used Python `pefile` + `capstone` (Binary Ninja is
GUI/personal on this machine — no headless API to script). Image base `0x00400000`,
primary code section `.text` VA `0x00401000`–`0x0051D000`. Scripts persist in the
repo at `tools/re-static/` (`sh3re.py` AOB + uniqueness, `chains.py`
xref/func-start/callers, `probe.py` singleton-offset tally — see its README);
re-run against a new build by pointing `EXE=` at its `sh3.exe`. AOB uniqueness =
count matches over `.text` only. Caution: raw 4-byte-immediate scans yield false
"refs" (e.g. `add eax, 0x566A28`); filter to instructions whose *memory operand*
disp equals the target (as `chains.py` does). **The 2026-08-17 pass (§6) extended
this to also scan `.pcode`** (h.sie's appended cave region, `0x00633000`+) — several
of the mods that reference `0x5543DC` live there, not in stock `.text`, and a
`.text`-only scan misses them entirely.

## 6. Static lead (2026-08-17): `0x5543DC` as a candidate player/equipment root

Follow-up static pass, run against `C:\games\OneAlex Extended 1.5.2\sh3.exe`
(current sha256 `6659539e…` — **drifted from the `oae-builds.md` `4B6BEE20` row**;
see that file's update). Motivated by depth-pressure-stress tickets `08`, `09`,
`11` all needing a player-object root and `0x5543DC` already being flagged, in three
separate notes, as an unidentified singleton used by stock depth code, the diesel
cave, and the u-tanker hull-repair cave.

**Sanity check first:** `004E1C99` (hull-HP getter) and `00513FE9` (depth setter)
are byte-identical to the 2026-07-21 pass despite the file hash drift — confirms
stock `.text` is still stable enough to trust this note's existing AOB anchors
without re-deriving them.

### 6.1 `0x5543DC` has exactly one writer in the whole binary

```
004A2966  A3 DC 43 55 00   mov [0x5543DC], eax
```

Immediately after, the same function walks an array of `0x54`-byte elements
looking for a match on `eax` (`imul esi, esi, 0x54` / `add esi, ecx`) — the shape
of a "find this object's slot in a list" idiom, consistent with registering a
newly-controlled unit. **Working hypothesis: `[0x5543DC]` is the live
player-controlled-unit pointer**, written once per mission/unit-change, read
everywhere else. Not yet proven — the registration function itself wasn't traced
to a name or call site this session.

### 6.2 It is read at both of stock's depth sites, not just as a stray guard

| Site | Context |
|---|---|
| `00513FCC` (inside the depth-setter `00513FC0`, 2 instructions before `ecx+0x54` is written) | `fld dword [eax+0x10]` off `[0x5543DC]` — a tolerance/epsilon float gating whether the setter bothers writing at all. |
| `00514560` (the depth-setter's caller region, matches the `this=esi` call site already recorded in §1/§2) | `[0x5543DC]` null-checked as a guard (`test eax,eax; je skip`) before calling the depth-update chain — **not** used as the `this` itself; `esi`/`ecx` (a parameter, still needing a dynamic root) is. |

So `0x5543DC` does **not** trivially equal `movementObj` — it's read from the same
function neighbourhood as a companion/guard object, which is still a useful
triangulation point for a CE pointer-scan (search near it) but not a proven
shortcut.

### 6.3 `[[0x5543DC]+4]` is a large, multi-mod-shared object — offset survey

Scanning every real xref to `0x5543DC` across `.text` **and** `.pcode`, then
following each `mov reg,[0x5543DC]` / `mov reg2,[reg+4]` pair and tallying what
gets dereferenced off `reg2`:

| Offset | Seen | First known use |
|---|---|---|
| `+0x004` | 19× | (the hop itself — object-of-object) |
| `+0x008` | 15× | `00635063` diesel cave: singleton-liveness check before the tick |
| `+0x010` | 4× | `006350D0` diesel cave region |
| `+0x014` | 2× | `0063521E` (unidentified h.sie cave) |
| `+0x018` | 2× | `0063505D` diesel cave: surface/running gate, summed with `+0x1C` (§4 of `04-diesel-damage-signature.md`) |
| `+0x01C` | 2× | same gate |
| `+0x084` | 3× | `00637153` (unidentified cave) |
| `+0x0A4` | 3× | `00637153`: **`call [eax+0xA4]`** — a vtable-style virtual call, i.e. this object has function pointers, not just data |
| `+0x0D8` | 1× | `00635640` (unidentified cave) |
| `+0x30C` | 3× | `0063505D` diesel cave: the confirmed port/starboard engine-damage floats |
| `+0x320` | 4× | `0063505D` diesel cave: the confirmed machine-telegraph float |
| `+0x358`/`+0x360` | 1× each | `00634371` (unidentified cave), `qword` (double) fields |
| `+0x438`/`+0x440` | 1× each | `006355F1` (unidentified cave) |
| `+0x4A0`/`+0x4A4` | 2× each | `006355F1` (unidentified cave) |
| `+0x83C` | 1× | `00635640` (unidentified cave) |

**`+0x83C` is the same offset** the 2026-07-21 pass found heavily used (641×) off
`[[0x5449B0]+0xD8]`, the "god"/manager object candidate already on record in §2.
That is independent circumstantial evidence that `[[0x5543DC]+4]` and
`[[0x5449B0]+0xD8]` may be **the same object reached by two different static
paths** — worth a direct pointer-value comparison in CE (dump both live and
compare) before spending a session on a from-scratch pointer-scan.

### 6.4 What this narrows, concretely

- **Ticket `11` (electric-engine float):** if the hypothesis in 6.1/6.3 holds, the
  electric-engine pair is very likely **another field on this same `0x30C`-region
  array**, not a separate object needing its own scan. The diesel pair occupies
  `+0x30C`/`+0x310` (two consecutive floats, port/starboard); the electric pair is
  the cheapest thing to check for immediately adjacent or structurally parallel
  (e.g. a second `[reg]`/`[reg+4]` pair a fixed stride away — try the damage-control
  screen's tab order or `en_menu.txt` string IDs as a cross-check for which slot is
  which).
- **Tickets `08`/`09` (movementObj / boat pointer for depth + hull HP):** `0x5543DC`
  is not proven to equal either pointer, but §6.2 shows it's read from the exact
  function neighbourhood both chains live in — a CE session should dump
  `[0x5543DC]`, `[[0x5543DC]+4]`, and compare both against whatever `ecx`/`esi`
  resolves to at `00513FC0` and `004E1C00`, **before** falling back to a blind
  pointer-scan from the HP mirror or the depth gauge.
- **Ticket `10` (flooding model) and mission clock:** no static candidate found this
  session. `+0x358`/`+0x360` are `qword` (double-precision) fields on the same
  object — doubles are unusual enough in this codebase to be worth a look (a
  mission clock as a double would fit), but this is speculative, not a lead.
- **Nine offsets above are still unidentified** (`+0x14`, `+0x84`, `+0xA4`, `+0xD8`,
  `+0x358`, `+0x360`, `+0x438`, `+0x440`, `+0x4A0`, `+0x4A4`, `+0x83C`) — listed so a
  future static or dynamic session doesn't re-derive the tally from scratch.

Method: `tools/re-static/` scripts extended to scan `.pcode` alongside `.text`; ad
hoc script not yet folded back into the checked-in tools (§5) — the offset-tally
logic in `probe.py` is reusable but was re-targeted at `0x5543DC` inline rather than
parameterized. Worth generalizing if another singleton needs the same treatment.
