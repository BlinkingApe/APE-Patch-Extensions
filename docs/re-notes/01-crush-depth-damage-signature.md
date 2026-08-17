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
worklist for **depth-pressure-stress** — kept current here as a single glance-view
across all four open tickets (`08`–`11`); findings themselves stay in their source
notes (this file §1–§9, or `06-flooding-model-signature.md` for flooding) and are
not restated below, only gisted:

| # | Value | Offset / chain | Status | Ticket |
|---|-------|----------------|--------|--------|
| 1 | **Player boat object → hull HP** | HP = `[boat + 0xB8]` | **code anchor + AOB DONE** (§1); HP-value *read* also available with **no chain** via static mirror `[0x566A28]`. Boat *pointer* (for writes) — **fully static chain found (§7): `[0x554A88]→+0xC4→+0x2240→+0x24→+0x4C`**, cross-referenced from the u-tanker hull-repair cave, which already writes `[boat+0xB8]` through it in shipped code. Chain-discovery is done without a debugger; live confirmation (write sticks under `ape.act`, one depth-charge delta) still needed. | `09` |
| 2 | **Current depth** | `[movementObj + 0x54]` | **code anchor + AOB DONE** (§1); `movementObj` pointer needs the dynamic chain-walk (§4) — **no static root exists (§8 confirms, exhaustively): the depth-setter's caller has zero direct callers, reached only via an untraceable vtable slot.** A from-scratch CE pointer-scan is required; no further static session should look for a shortcut. | `08` |
| 3 | ~~CDT (crush depth)~~ | — | **dead.** Tier model never reads crush depth (map, Out of scope). | — |
| 4 | **Mission clock** | TBD | **dynamic hunt (§4)** — no static candidate found in two passes (§6, §8); §8 ruled out and reassigned the one candidate double-field pair. Needs a live Exact/Increased-Value scan from scratch. | `08` |
| 5 | **Astern seepage / flooding trigger** | TBD | **dynamic hunt, two unconfirmed leads (`06-flooding-model-signature.md`):** SH3's own interior-camera data tags its sternmost compartment `"DIESEL ENGINE ROOM;InteriorAft"` (evidence for which compartment is astern); a flooding-message read/compare (`[ecx+0x1C]` vs `0.35` at `0x436BE4`) has an unresolved `ecx` worth watching live. No h.sie precedent to borrow, unlike hull repair/diesel damage — genuine discovery, not confirmation. | `10` |
| 6 | **Electric-engine damage float** | `[[0x5543DC]+4]+0x358`/`+0x360` (lead, unconfirmed) | **dynamic hunt.** §6's original offset survey was mostly a scan artifact — corrected in §9: most "unidentified" offsets belong to *other* objects, and the two real remaining candidates (`+0x438`/`+0x440`; UI strings 462/463) are both ruled out. `+0x358`/`+0x360` (a bounded max/current double accumulator, §8) is the only static lead left — unconfirmed, not the diesel pair's shape (doubles, not two floats). | `11` |

**Static pass complete (2026-07-21), extended across five follow-up passes
(2026-08-17, §6–§9 plus `06-flooding-model-signature.md`).** The original two code
anchors (§1) are located with verified-unique AOB signatures. Ticket `09`'s pointer
chain is fully resolved statically (§7). Tickets `08` and `11` have been run down as
far as static analysis goes — `08` has **no** shortcut (confirmed, not just
unfound); `11` has one unconfirmed lead left after most of its candidate offsets
turned out to be scan artifacts (§9). Ticket `10` started from zero and has two
open leads. The AOB signatures are the self-verification handles ADR-0003 calls
for — once CE gives a static→object chain, the matching AOB confirms the field
offset is still valid for this build.

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
  - `0x00554A88` — **a fourth, independent root, found 2026-08-17 (§7) not by
    scanning but by cross-referencing this repo's own `03-utanker-hull-repair-
    signature.md`.** ~79 real `[0x554A88]` memory-operand reads across stock
    `.text` alone (far more than any of the three above), and the full chain
    `[0x554A88]→+0xC4→+0x2240→+0x24→+0x4C` is **proven to equal the player boat
    object** by shipped, working game code — h.sie's hull-repair cave uses exactly
    this chain to compute `ebx` and then reads/writes `[ebx+0xB8]`/`[ebx+0xB4]`
    (hull HP / max hull), the same fields this note's §1 derived independently from
    the HUD-mirror getter. Not near any of the other three roots (offsets
    `0x100D8`/`0x9FC`/`0x6AC` away) — a genuinely separate path to the same object.

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

## 7. Static lead (2026-08-17, second pass): `0x554A88` boat-pointer chain, found by cross-referencing this repo, not by scanning

Prompted by a re-audit of the map for stale information: ticket `13`/`15` retired
the *code* for the old model, but the depth-pressure-stress note's own candidate-
root list (§2/§6) had never been cross-checked against **this repo's own other RE
notes**, which already document a fully-resolved, working pointer chain to the
player boat object. That chain is
[`03-utanker-hull-repair-signature.md`](03-utanker-hull-repair-signature.md) §4
"Block 2 — HULL REPAIR", found weeks earlier by static diffing for a different
ticket and never connected back here.

**The chain:** `[0x00554A88] → [+0xC4] → [+0x2240] → [+0x24] → [+0x4C]` = player
boat object (`ebx` in the disassembly below).

**Re-verified 2026-08-17 against the current install**
(`C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256 `eeef8595…`, distinct from both
this note's `4B6BEE20` build and the `6659539e…` hash §6 used — a third hash for
the same install path in five weeks; see `oae-builds.md`). The hull-repair cave is
present, byte-identical in the chain-resolution instructions, at the same VA
`00633633` `oae-builds.md` already registered as stable across all four surveyed
binaries:

```
00633633  8b1d884a5500       mov ebx, dword ptr [0x554a88]
00633639  8b9bc4000000       mov ebx, dword ptr [ebx + 0xc4]
0063363F  8b9b40220000       mov ebx, dword ptr [ebx + 0x2240]
00633645  8b5b24             mov ebx, dword ptr [ebx + 0x24]
00633648  8b5b4c             mov ebx, dword ptr [ebx + 0x4c]
...
00633663  d88bb4000000       fmul dword ptr [ebx + 0xb4]      ; x MAX hull
00633669  d883b8000000       fadd dword ptr [ebx + 0xb8]      ; + CURRENT hull
0063366F  d993b8000000       fst  dword ptr [ebx + 0xb8]      ; STORE hull HP
```

**AOB (literal, 24 bytes, the five-instruction chain-resolution block only):**
`8B 1D 88 4A 55 00 8B 9B C4 00 00 00 8B 9B 40 22 00 00 8B 5B 24 8B 5B 4C`
— **unique, 1 match, over the whole file** (not just `.text`; checked both
`tools/re-static` regions and `.pcode`/`.pconst`/`.pdata`, since the cave itself
lives in `.pcode`). No wildcards needed — same reasoning as §1's anchors: this is
inside the hull-repair cave `oae-builds.md`'s cross-build survey already confirmed
present at an identical offset across OAE 1.5.2, OAE 1.5.3.7, KC 2.5, and GWX
3.0 Gold.

**Why this is strong evidence, not just a guess:** `[ebx+0xB8]`/`[ebx+0xB4]` are
the *exact same fields* §1 derived independently from the hull-HP HUD-mirror
getter (`004E1C99`). Two unrelated code paths — the stock getter and h.sie's
hull-repair patch — agree on the same struct layout for the same object. And
`[0x554A88]` is read directly (`mov eax,[0x554A88]`, no chain at all) at **~79
sites in stock `.text`** in this build, overwhelmingly more than any of the three
singleton candidates in §2 — consistent with it being a very central, frequently-
touched root (plausibly "the current player unit" or similar), not a niche guard.

**What this leaves for ticket `09` (still HITL):**
- The **pointer-chain hunt itself is done** — no CE pointer-scan should be needed;
  the acceptance criterion "resolved via a chain rooted in stable stock sh3.exe" can
  likely be satisfied by *reading* this chain live and confirming it matches
  whatever `ecx` resolves to at `004E1C00`, rather than a from-scratch scan.
- **Still needs live confirmation**, not because the chain is in doubt, but because
  the map's own bar (ticket `09`'s acceptance criteria) is a *live* write-sticks
  check plus one observed depth-charge HP delta, and h.sie's cave proves the write
  sticks for *his* trigger (docking), not for a novel writer like `ape.act`'s own
  loop thread touching the same field on its own schedule.
- Does **not** resolve ticket `08` (movementObj/depth, or the mission clock) — the
  u-tanker note itself flags (§4, "Not yet established") that this chain, the
  `0x5543DC` chain, and `0x5449B0` are three distinct routes to player-adjacent
  objects that were never reconciled; nothing here proves `0x554A88`'s resolved
  object also exposes `+0x54` = depth.

Method: `tools/re-static/chains.py`'s `real_mem_refs` pattern, re-run manually
against the current install and against the whole file rather than `.text` only
(script not yet folded back into the checked-in tool — same caveat as §6's method
note).

## 8. Static lead (2026-08-17, third pass): `movementObj` stays unrooted; a §6.3 offset reassigned

Follow-up static pass targeting ticket `08` specifically: does the `0x554A88` boat
chain (§7) also reach `movementObj` (the `+0x54` current-depth field), sparing a
second CE pointer-scan? Run against the same install
(`C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256 `eeef8595…` — unchanged from §7,
no further drift).

**Result: no.** Exhaustively checked both directions:

- **Forward from `0x554A88`:** all 20 real xrefs to `0x554A88` across
  `.text`+`.pcode`+`.pconst`+`.pdata`, windowed ±15 instructions each, for any
  `[reg+0x54]` operand. Zero hits.
- **Backward from the chain's last hop:** all 6 real `mov reg,[reg2+0x4C]` sites in
  the whole binary (the chain's final step) — all 6 are h.sie `.pcode` caves, none
  in stock `.text`; four are simply other h.sie mods reusing the identical
  `0x554A88` chain to reach `[boat+0xB4]`/`[boat+0xB8]` (hull), never `+0x54`.
  Traced each forward 40 instructions past the `+0x4C` hop — no `+0x54` follow-up
  anywhere.

So the boat object (hull HP, `0x554A88` chain) and `movementObj` (depth, `+0x54`)
remain **not shown to be the same object** — plainly negative, closing off what
would have been the cheapest possible shortcut for ticket `08`.

**The depth-setter's caller is unreachable statically, definitively.** Full
disassembly of `00514560` (the setter's caller, `esi`/`ecx` = `this` passed straight
through to `00513FC0` and siblings `005139F0`/`00514470`) shows it has **zero direct
(`E8`) callers anywhere in the file** — it's reached only via a vtable slot at
`.rdata:0x005322E0`, and that vtable's base has zero raw references anywhere either.
This is consistent with §3's finding that damage/action dispatch runs through a
generic virtual-call mechanism (`sObject::Action`-style), not a resolvable call
chain. **This closes the static path, rather than narrowing it** — `movementObj`'s
root cannot be found without a live pointer-scan; no further static session should
spend time on it. One minor triangulation point for that CE session: `[this+0x60]`
is a small refcount/tick guard read in the setter (`00513FD4`) and touched in the
caller (`0051457F`) and in the sibling `00514470` — not a root, but useful to watch
alongside `ecx`/`esi` when dumping live.

**§6.3's `+0x358`/`+0x360` doubles are ruled out as the mission clock, and
reassigned.** Traced to their actual code (`00634371`, `006335FA`–`00633615`):

```
mov ebx,[0x5543DC]; mov ebx,[ebx+4]
fmul qword [ebx+0x358]      ; rate * something
fadd qword [ebx+0x360]      ; += current
fst  qword [ebx+0x360]      ; store back (accumulate)
fcomp qword [ebx+0x358]     ; compare to a cap
jb ...                      ; clamp if not yet at cap
```

This is a **bounded accumulator with a max cap**, not a monotonically-increasing
clock — the shape reads as an equipment wear/damage-ratio pair (max/current,
divided for a percentage at `00634371`, gated on the same `[0x5543DC+0x10]`
liveness tolerance §6.2 already found), not a time value. Flagged for ticket `11`
(electric-engine wear) as a possible lead — **not chased further, out of this
ticket's scope**. No other mission-clock candidate found; two static passes now
agree there is none to find statically (§6, §8) — it needs a live Exact/Increased-
Value scan from the start, same as always planned.

**What this leaves for ticket `08` (still fully HITL):** both live values need a
from-scratch CE pointer-scan/value-scan; no static shortcut exists for either. This
is a genuinely negative result, recorded so a future session doesn't re-spend time
looking for one.

## 9. Static lead (2026-08-17, fourth pass): §6.3's offset table corrected — most entries were mis-attributed by register reuse

Follow-up static pass chasing ticket `11` (electric-engine damage float), against
the same unchanged build (`eeef8595…`). Re-walked every `mov reg,[0x5543DC]` →
`mov reg,[reg+4]` pair with a **stricter** tracker that stops following a register
the instant it's reloaded from anything other than itself. §6.3's original tally
did not do this, and it shows: **most of that table's "unidentified" offsets do not
actually belong to `[[0x5543DC]+4]`.**

### 9.1 Corrected offset list

The only offsets genuinely dereferenced off `[[0x5543DC]+4]` in `.text`+`.pcode`:

`+0x000, +0x004, +0x008, +0x30C` (diesel pair, confirmed), `+0x320` (telegraph,
confirmed), `+0x358`/`+0x360` (§8's wear-ratio doubles, still the only live lead),
`+0x438`, `+0x440`.

**Struck from §6.3's table — confirmed to belong to *other* objects, traced by
hand:**

- `+0x084`/`+0x0A4` (`00637153`): `eax` is reloaded from a **different singleton,
  `[0x6307BC]+4]`**, before these offsets are touched — §6.3's tally kept the stale
  "off `0x5543DC`" label past the reload. `+0xA4` is a generic virtual dispatch
  (`call [vtable+0xA4]`, passing the telegraph value and `-1`) — a method call, not
  a data field, and not on this object at all.
- `+0x0D8` (`00635640`): never actually referenced in that function — a scan
  artifact.
- `+0x4A0`/`+0x4A4` (`00635640`): actually off **yet another singleton,
  `[0x5546C4]+4]+4]`** (a double `+4` hop, not `0x5543DC`), guarded by flag
  `0x63CF04`, zeroing an unrelated `[ebx+0xB4]` ("max" field, u-tanker-chain
  convention) on two object pointers — a different h.sie patch entirely.
- `+0x83C` (`00635640`): the already-known god-object field off `[0x5449B0]+0xD8]`
  (§2/§7) — 600+ real hits across stock `.text` alone, a generic property slot, not
  engine-specific, and not actually off `0x5543DC`.
- `+0x014` (`0063521E`): re-traced as `[0x5543DC]+0x10` directly (the already-known
  liveness-tolerance field, §6.2), inside an unrelated counter/gate block (caps a
  tick counter at 10, resets a flag) — no adjacent damage float, and not a distinct
  `+0x14` offset at all.

### 9.2 `+0x438`/`+0x440` ruled out — a torpedo-reload flag, not electric engines

```
006355F1  mov eax,[0x5543DC]; mov eax,[eax+4]
006355F9  mov bh, byte [eax+0x440]        ; BYTE flag, not a float
006355FF  mov bl, byte [0x63E130]         ; cached previous value
00635605  cmp bx, 1                       ; new=0, old=1 -> transition off
0063560A  jne 0x635616
0063560C  mov dword [eax+0x438], 0x3F000000  ; hard-set to exactly 0.5
00635616  mov byte [0x63E130], bh
```

`+0x440` is a byte flag, not a float; `+0x438` is force-set to a fixed `0.5` on one
specific transition — no clamp idiom, no `0.0..1.0` accumulation, flag+float rather
than the diesel pair's two-adjacent-floats shape. The nearest message block above it
(string `0x12D3` = "Reloading external torpedoes") places this in a torpedo-reload
state patch. **Ruled out.**

### 9.3 String IDs 462/463 ("Stb/Port Electric Engine") — negative, and solidly so

Searched `.text`+`.pcode` for both `push imm32` (the diesel/torpedo message
convention, e.g. `push 0x131D` for string 4893, §2) and `mov reg32,imm32` (used
elsewhere in these caves for string IDs like `0x12D2`/`0x12CE`) pushing `0x1CE`
(462) or `0x1CF`(463). **Zero hits, either form, anywhere in the binary.** Diesel's
`460`/`461` do turn up 3× in stock `.text` (not `.pcode` — likely the
damage-control station-list UI, not a cave) — electric engines have no comparable
occurrence at all. Unlike diesel, flooding (§ note 06), or torpedo reload, **there
is no UI-string anchor to chase for electric engines in this build.**

### 9.4 Net for ticket `11`

`+0x358`/`+0x360` (§8) remains the **only** static lead — unchanged, not promoted
or demoted by this pass. Everything else chased this session was ruled out for a
specific, citable reason rather than left an open question. The offset survey
table in §6.3 should be treated as corrected by this section, not superseded
wholesale — the confirmed fields (`+0x30C`, `+0x320`) and the still-live lead
(`+0x358`/`+0x360`) are unchanged.
