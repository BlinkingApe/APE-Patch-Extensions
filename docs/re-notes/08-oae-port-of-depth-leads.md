# RE note: porting the sh3-crush-pressure leads to OAE (static half)

Tickets `10` (stern water, pump rate), `11` (electric-engine damage, repair threshold)
and `17` (crush depth, crush speed, crush onset), in one session, 2026-11-25.

**Source build (the leads):** unwrapped vanilla Steam SH3 1.4b, sha256
`1f14568235f903e7e50b73d3caa937b6946aadf7013e3dc39dfd1cc7975986f2`
(`d:\code\sh3-crush-pressure`, `docs/findings/*.json`).
**Target build:** `C:\games\OneAlex Extended 1.5.2`, hashed at the start of this session:

| File | sha256 |
|---|---|
| `sh3.exe` | `20360fe8398692172c7cc36e4337f1d5936ee9c9e0eac97e4c3214b2eb132561` |
| `SH3Collisions.act` | `7b1b4f3532c2336f58ae4723864d09eb9de79be94b6d3a8060d95b5c3850bb20` |
| `sh3sim.act` | `f5c9d9804a554630304ad7af0ef8e3943720f305346797fc5f42a6d784787f45` |
| `data/Zones.cfg` | `4c4122cc3c6e3355ca0c8eeb6b7e36dd79cc202bc0d6cf8f081755178506ce4e` |

`sh3.exe` has **drifted again**: the August live sessions (tickets 08, 09, 11) used
`eeef8595…`. Every address below was found again in this file by AOB, not copied.

**Method.** Every AOB in the lead's finding records was counted in the vanilla and
the OAE copies of `sh3.exe`, `SH3Collisions.act` and `SH3Sim.act`. Then each module's
`.text` was diffed byte for byte against vanilla (the sections are the same size and
base), and each differing run that falls inside a function the leads rely on was
disassembled. Scripts: throwaway, in the session scratchpad. They aren't kept.

## 1. Anchors: all but two port unchanged, at the same VA

Every anchor of `stern-water`, `pump-rate`, `electric-engine`, `repair-threshold`,
`hull-hp`, `hull-hp-writers`, `hull-hp-mirror` and `depth` matches in OAE **at the
same virtual address as vanilla**, with the same match count. That includes:

- stern-water list head `mov edi,[0x5547D4]` @`0x0043601D`, the flood integrator
  `SH3Collisions+0x77B9`, the pump store `0x00429C98`;
- the electric-engine root `mov [0x5543DC],esi` @`0x004A28B6`, the `+0x37C` offset
  @`0x004C14D1`, the zone → equipment table read @`0x004360EC`;
- the repair gate `0x0042A2D0` and the crew-strength chain @`0x00435ACB`;
- the crush writer `SH3Collisions+0x30D7`, the hit writer `+0x36D7`, the load/init
  writers `+0x2376…+0x2383`, `+0x2919`, `+0x4E3C`/`+0x4E48`.

Only two anchors miss. Both are h.sie caves (§2, §3).

**The diff confirms it.** `sh3.exe` `.text` differs from vanilla in 108 runs. Only two
land in a function the leads use: `0x0041DFB6` (repair step) and `0x004C2115` (mission
clock store). `SH3Collisions.act` differs in 2 runs, one cave (§4), outside the crush,
flood and apply-damage functions. `sh3sim.act` differs in 15 runs. None is in the
propulsion damage read `0x1000A017`. The nearest, `0x1000A508`, is a cave on a
different computation (`[esi+0x3A8]`), not traced.

**Zone indices are unchanged.** OAE's `Zones.cfg` differs from vanilla, but the zone
list keeps `22=ElectricEngine1`, `37=TorpedoRoomStern`, `56=ElectricEngine2`. So the
lead's matches by zone index (stern water: zone 37; engines: 22 and 56) port as-is.

## 2. The repair step: h.sie's Realistic Repair Times divides it by 60

Vanilla `0x0041DFB6` is `fsubr [ecx]; fst [ecx]; fcomp [0.0]`. OAE replaces it with
`jmp 0x0063311F`:

```
0063311f  cmp dword [0x63e120], 3 ; jge +            ; [0x63e120] forced up to 3 (not traced)
00633132  cmp byte  [0x63cf00], 0xff ; jne skip      ; h.sie flag 1, Realistic Repair Times
0063313b  cmp ebx, ecx ; jne skip
0063313f  fdiv dword [0x63c000]                       ; 60.0  (hsie.ini Val1=60.0)
00633145  fsubr [ecx] ; fst [ecx] ; fcomp [0.0] ; jmp 0x41dfc0
```

Flag 1 is `0xFF` (on) in this install. Both callers of the step (`0x00429E9B`,
`0x0042A19D`, the gate's repair call) pass the damage pointer in `ebx` as well as on the
stack, so `ebx == ecx` holds and **every repair step is divided by 60**. The clamp to 0
and the gate's `0 < d < 1.0` test are untouched.

- **The threshold ports: repairable strictly below 1.0.** The gate `0x0042A2D0` is
  byte-identical. The 0.9 hard ceiling keeps its margin.
- **The repair rate does not port.** Vanilla measured 0.00234 damage/game-s (~427
  game-s per 1.0). Statically, OAE is **60× slower: ~3.9e-5/game-s, ~7.1 game-hours
  per 1.0 of damage** for the same crew. Not yet measured live.
- **Consequence for ticket 12.** Its tail simulation ("watch crew repairing, the worse
  engine peaks at 0.44 median, touches the ceiling in 0.24 % of 4-hour patrols") assumed
  the vanilla rate. On OAE repair barely races wear, so the unrepaired figures apply
  instead: about 99 % of 4-hour tier-2 patrols reach the 0.9 ceiling. Repair from the
  ceiling takes ~6.4 game-hours. It is still possible, so the repairability guarantee
  holds, but the tuning conclusion changes.
- **The pump is not caved.** The pump step `0x00429C40…0x00429C98` is byte-identical,
  so the pump formula and rate port: `factor × (crew + team strength) × 0.2/60`, 0.00243
  flood/game-s for a fresh VIIC stern crew. The pump does not go through `0x0041DF60`.

## 3. The mission clock: h.sie's mirror is the stock clock, by construction

Vanilla stores the clock with `mov [esi+0x150],eax` @`0x004C2115`. OAE jumps to
`0x0051CFC7`:

```
0051cfc7  mov [0x0063ff80], eax
0051cfcc  mov [esi+0x150], eax
0051cfd2  jmp 0x004c211b
```

The mirror and the stock clock get the same register in consecutive instructions.
`0x0063FF80` has **no other reference** in `sh3.exe`. The only other module that names it
is `sh5.act` (h.sie's moon mod), in a table of consecutive `0x63FFxx` addresses, which
reads like a reader, not a writer. Not traced further.

**This statically settles the map's open save-load question.** "Does h.sie's
`0x0063FF80` also read 0 through the restore?" Yes, as far as the stock clock does,
because the two can't differ. Guard 2 (don't amplify a drop when the clock read 0 at the
previous poll) can use `0x0063FF80`. The caveat: `sh5.act` was not checked for writes.

## 4. SH3Collisions: one cave, on the ram path

`SH3Collisions+0x1460` → `+0x18050`. For the player boat (matched through ticket 09's
`[0x554A88]→+0xC4→+0x2240→+0x24→+0x4C` chain) and one caller (`sh3.exe` return
`0x004D741F`), it multiplies a damage value `[ecx+0x2C]` by `[0x63C0C4]`. That is h.sie's
"Collision Damages Fix" (hsie.ini `Val17`; the exe holds 8.0, the ini says 16.0). It is
on the hit/collision path, not the crush path. For poll-and-amplify it means ramming
damage to the player is already ×8 before APE multiplies it.

## 5. Crush: the field offsets port; the onset formula, from the code

The crush function `SH3Collisions+0x2E70…+0x30FD` is **byte-identical** to vanilla, so
`+0xC0` (CrashDepth) and `+0xC4` (CrashSpeed) port. Reading it gives the onset and
the rate the lead left unexplained ("how the routine gets 247 m isn't worked out"):

```
h      = hull / max hull                         ; [obj+0xB8] / [obj+0xB4]
E      = CD > 1 ? 1 + (CD − 1)·h : CD·h          ; CD = [obj+0xC0], .zon CrashDepth / 10
onset  = 0.7 · E                                 ; 0.7 = [SH3Collisions+0x19B40]
D      = depth of a hull reference point, in the same tenth-units
if D > onset:
    hull −= CrashSpeed · dt · (D − onset) / (E − onset)      ; [obj+0xC4], cause 4
```

(The loop just before it applies the same shape to each zone, with its own
reference point, cause 4.)

**It agrees with every vanilla measurement:**

- Full hull, VIIC, `.zon` CrashDepth 346: onset = 0.7 × 34.6 = 24.22 units =
  **242.2 m** at the reference point. Measured onset on the depth gauge was 247.3 m. So
  the reference point is ~5 m shallower than the gauge's depth, at that attitude.
- Per-frame loss at 250–252 m gauge: CrashSpeed 2 × (2.7–4.7 m)/(103.8 m) ÷ 60 fps ≈
  0.0009–0.0015 HP. Measured: 0.0009–0.0014.
- At 0.6 hull: onset = 0.7 × 10 × (1 + 33.6 × 0.6) ≈ 148 m (~153 m gauge). Measured:
  firing at 172–175 m, consistent.

**What this changes:**

1. **Ticket 17's boundary is `0.7 · E`, not `CD`.** It depends on hull, every poll.
   APE can compute it from fields it already reads (`+0xB4`, `+0xB8`, `+0xC0`). But
   stock tests a hull reference point, not the gauge depth APE reads
   (`movementObj+0x54`). Those differ by ~5 m on a vanilla VIIC. The offset per class
   and attitude, and whether "on the same frame" needs that point, are live questions.
2. **`CrashSpeed` is HP per second only at `D = E`.** At onset the rate is 0. It ramps
   linearly through `E` (rate = CrashSpeed) and keeps growing below. As hull falls, `E`
   and the onset rise, so a boat held at one depth dies **faster and faster**. This is a
   finer reading than ADR-0010's basis ("`CrashSpeed` is hull HP per second", the
   owner's VIIC test: 500 HP, 0.8 → ~625 s) and than the lead's `excess × CrashSpeed ×
   dt`. It's consistent with the owner's figure only if the test boat was deep enough,
   on average, for the ramp to average ~1. **Not re-decided here.** It bears on what
   ticket 16's "crush time" means to a player, so it's for the owner.
3. **APE's crush-damage exemption needs the ramp.** ADR-0010 exempts `crush speed ×
   dt` of each drop. The true crush share is `CrashSpeed · dt · (D − onset)/(E −
   onset)`, zero near the onset. An exemption of `CrashSpeed · dt` over-exempts there,
   and under-exempts deeper than `E`. An alternative signal: the crush writer stamps
   `[obj+0x170] = 4` (hit writer: 1 or 3), but that is last cause only, and a hit plus
   crush in one poll would be ambiguous.

**The owner's position (2026-11-25), recorded as given:** after many hours in game, the
owner has only ever seen a flat crush loss, whatever the depth; with a low enough
`CrashSpeed` a boat can sit at any depth for hours and lose HP at a constant rate. The
owner asked that the flat rate be assumed and the plan kept. §7.3 measured the opposite
on OAE, live. That conflict is for the owner to settle; nothing here changes ADR-0010 or
ticket 16.

## 6. What only a live OAE session could settle (planned before §7)

| Ticket | Live check left |
|---|---|
| 10 | Chain resolves on OAE (list head → zone 37 → `+0x1C`); a write holds and shows in F7; pump rate measured (statically 0.00243/game-s fresh, the code being unchanged). |
| 11 | Port/stb chains resolve; a write holds and shows in F7; repair at 0.99999994 succeeds; **repair rate measured, expected ~3.9e-5/game-s** (§2). |
| 17 | `+0xC0` = OAE `.zon` CrashDepth / 10; `+0xC4` = the `.zon` CrashSpeed after a mission load (ticket 16's value); writer onset vs `0.7·E` at full and ~60 % hull; the gauge-to-reference-point offset. |

## 7. Live half, 2026-11-25: OAE lab, sh3-crush-pressure harness

**How it ran.** The sh3-crush-pressure harness, ported to OAE on its branch `oae-port`
(an `SH3_LAB=oae` profile). It ran against a **robocopy of the owner's install** at
`d:\code\sh3-crush-pressure\installs\OAE 1.5.2`, not the install itself. The lab
`sh3.exe` hashes `20360fe8…`, as above. Every run checked that the owner's install,
`Documents\SH3` (session window, hash-verified restore), the Steam copy and the
registry were unchanged. The lab ledger holds three changes:

- `d3d9.dll` moved aside: its 1920 × 1080 display wrapper clips OAE's menus in the
  1024 × 768 window;
- the intro `Logo.wmv` moved aside;
- the two lab missions added.

Values were read and written through Frida (`harness.instrument`). Frida must attach
**at the main menu**: once a mission loads, the 32-bit game is at ~1.5 GB and the
attach fails ("unable to allocate target memory").

**The boat was a Type IIA every time.** Every single mission spawned a IIA: the lab's
(class `SSTypeVIIC`, tried with "U-LAB" and with roster name U-96, dated before and after
the VIIC's `AppearanceDate=19400730`) and stock Barham's U-331. F7 names it
"TypeIIA", and the live fields are the IIA `.zon`'s (Hit_Points 400, CrashDepth 251,
CrashSpeed 0.8). Only `NSS_Uboat7c` claims `ClassName=SSTypeVIIC`. **Cause not found.**
The figures below are therefore a IIA's. The chains, the write tests and the formulas
don't depend on class. Which compartment is "astern" does.

### 7.1 Ticket 10: stern water

- **Every chain resolves:** list head `0x005547D4` → zone states → `+0x1C`; the
  zone → compartment table `0x00566898`.
- **The IIA has 8 compartments, no zone 37.** Zones 31/42/32/29/30/36/24/25 =
  compartments 0–7. OAE's F7 orders them stern to bow: **electric engines (4)**,
  diesel (3), stern quarters (7), command (2), sonar/radio (1), bow quarters (6), bow
  torpedo (5). So the IIA's sternmost compartment is the **electric engine room**, not
  stern quarters (the vanilla lead said "II → 7").
- **Per class, by data:** OAE's `Basic.cfg` has vanilla's compartment ids. VII and IX
  have compartment 8 (SternTorpedo); II and XXI have no 8. Not seen live on a VIIC
  (above).
- **Write 0.5 to zone 30's flood: holds.** The crew pumped it (0.4967 → 0.2067 in 89
  game-s). F7 drew the water, CE reported "We have flooding, sir!", and the boat sank
  30 → 67 m.
- **Pump rate: 0.003258 flood/game-s** at crew strength 1.0 (team 0). The unchanged
  pump formula gives 1 × 1.0 × 0.2/60 = 0.00333, so the factor is 1: realistic repair
  time is on. A compartment with no crew (compartment 7 in run 2) wasn't pumped at all.

### 7.2 Ticket 11: electric engines

- **The chain resolves:** `[[[[0x5543DC]+4]+0x37C]+0/4]`, port and starboard.
- **Writes hold and show:** F7 shows damage icons on the electric-engine station.
- **0.99999994 is repaired on OAE** (0.99994 → 0.99511 in 89 game-s), so the
  threshold ports: repairable strictly below 1.0.
- **Repair rate: 5.43e-5 and 5.37e-5 damage/game-s** in two runs, at crew strength 1.0.
  Prediction (§2, h.sie's ÷ 60): 1.0 × 0.2/60/60 = 5.56e-5. **Confirmed: OAE repairs
  60× slower than vanilla**, ~5.1 game-hours per 1.0 of damage, ~4.6 from the 0.9
  ceiling.
- **One engine at a time.** In both runs the second engine's damage didn't move in
  ~90 game-s while the first was repaired.

### 7.3 Ticket 17: crush depth, crush speed, crush rate

- **`+0xC0` = 25.1** = the `.zon` CrashDepth 251 / 10; **`+0xC4` = 0.8** = the `.zon`
  CrashSpeed (the owner's test-mod value; ticket 16's patch isn't applied in this copy).
- **Rate at a fixed 200.0 m, hull written down in steps** (cause 4, crush, throughout):

| Hull, mid-window | Loss, HP/game-s | §5 ramp, reference point 197.4 m | Flat |
|---|---|---|---|
| 0.923 | 0.399 | 0.399 (fitted) | 0.8 |
| 0.780 | 0.827 | 0.79 | 0.8 |
| 0.554 | 1.770 | 1.80 | 0.8 |

  The ramp has one free constant: the reference point's depth, fitted from the first
  window as 2.6 m above the gauge. It then predicts the other two within 5 %. A flat
  rate fits none. On OAE, live, **crush loss is not flat**: it grows as hull falls, so a
  boat held at one depth loses hull faster and faster. Raw rows: harness work folder
  `work/oae-crush.json`.
- **Onset.** Not seen frame by frame. The fit puts it at `0.7·E` (§5) at all three hull
  values. The ticket's "same frame" criterion is met only to that extent.
- **h.sie's clock mirror read the stock clock's value** at every probe (§3).

### 7.4 What the port left undone

- **The harness can't yet replay OAE's menus unattended.** The menus kept vanilla's
  click targets once the wrapper was gone, but none of the 13 screen references has
  been re-recorded for OAE, and the first click on a fresh main menu is ignored.
  These runs were driven step by step (`harness.drive`) with throwaway probe scripts
  in the harness's `work/` folder.
- **Never seen on a VIIC**, because of the IIA substitution.
