# Diesel Damage Fix — patch site, model, and the `hsie.ini` runtime overlay

Found 2026-08-15 by static diffing + disassembly (method in
[`02-hsie-patch-identification-method.md`](02-hsie-patch-identification-method.md)),
then **confirmed line-for-line against h.sie's own documentation** in
`hsie.ini` — which turned out to state the damage formula outright.

Verified against `data/exe_with_hsie_patches/sh3_Diesel_Damage.exe` (sibling
build, see [`03-utanker-hull-repair-signature.md`](03-utanker-hull-repair-signature.md) §0)
and cross-checked against the live install `C:\games\OneAlex Extended 1.5.2\sh3.exe`
(sha256 `51d14ca3…`).

> ⚠ **The patch is OFF in every install surveyed.** Flag `0x63CF13` (table entry
> #20) is `0x00` in OAE 1.5.2, OAE 1.5.3.7, KC2.5, and the GWX folder. The model
> below is real but nobody on this machine is currently running it.

---

## 0. Headline: `hsie.ini` is live, and it overwrites `.pconst` at runtime

This is the finding with consequences beyond diesel damage.

`hsie.act` (41,472 bytes, present in the install root) contains the strings
`./hsie.ini`, `nParam`, `GetPrivateProfileStringA`, `VirtualProtect`, and
`VirtualQuery`. `hsie.ini`'s section 3 is an explicit parameter table:

```
nParam=24
Adr1=0x63C000    Typ1=float
Adr9=0x63C0A4    Typ9=float
...              Adr24=0x63C0E4
```

**So `hsie.act` reads `hsie.ini` at load, `VirtualProtect`s h.sie's `.pconst`
section, and writes each `ValN` float to `AdrN` in memory.**

Proof it is applied at *runtime*, not baked in at patch time — the on-disk exe
and the ini **disagree**, and the ini is the one that wins in play:

| Param | Address | Value in `sh3.exe` | Value in `hsie.ini` |
|---|---|---|---|
| Val2 (night vision) | `0x63C0E8` | 0.75 | **0.5** |
| Val3 (torp fail, ≤6/40) | `0x63C084` | 0.15 | **0.35** |
| Val4 (torp fail, 6/40–6/42) | `0x63C088` | 0.10 | **0.20** |
| Val5 (torp fail, ≥6/42) | `0x63C08C` | 0.03 | **0.05** |
| Val9–Val16 (diesel) | `0x63C0A4`–`0x63C0C0` | match | match |

### ⚠ Consequence for APE's static patcher

**Any static byte APE writes to an address in `hsie.ini`'s `Adr1`–`Adr24` list
is overwritten in memory by `hsie.act` at load.** The full clobbered set is:

```
0x63C000, 0x63C084, 0x63C088, 0x63C08C, 0x63C090, 0x63C094, 0x63C098,
0x63C0A4, 0x63C0A8, 0x63C0AC, 0x63C0B0, 0x63C0B4, 0x63C0B8, 0x63C0BC,
0x63C0C0, 0x63C0C4, 0x63C0C8, 0x63C0CC, 0x63C0D0, 0x63C0D4, 0x63C0D8,
0x63C0DC, 0x63C0E4, 0x63C0E8
```

The manifest format (ticket 08) needs to treat these as a **forbidden write
region** — verify-before-write cannot catch this, because the on-disk bytes are
correct right up until `hsie.act` loads.

**The hull-repair site is unaffected**: `0x63C404` and the `push 0x10` /
`add eax, 5` operands at `0x633650`/`0x633655` are all outside the list.
**Every diesel-damage tunable is inside it.**

---

## 1. The hook: a one-real-second tick

Trampoline `jmp 0x4D7195` at `0x00635040`; the cave proper is the wrapper at
`0x00635000`, entered from stock code near `0x4D7190`.

```
00635000  d9 05 2ce16300   fld   dword [0x63E12C]     ; accumulator
00635006  d8 44 24 08      fadd  dword [esp+8]        ; += frame delta
0063500A  d9 15 2ce16300   fst   dword [0x63E12C]
00635010  d9 e8            fld1
00635012  df f1            fcomip st(1)               ; 1.0 vs acc
00635016  77 21            ja    0x635039             ; < 1s -> skip
00635018  c7 05 2ce16300 0 mov   dword [0x63E12C], 0  ; reset
00635022  83 3d dc435500 0 cmp   dword [0x5543DC], 0  ; singleton present?
0063502B  83 3d 98465500 0 cmp   dword [0x554698], 0
00635034  e8 10000000      call  0x635049             ; <- the diesel tick
00635039  66 8b 05 44665600 mov  ax, word [0x566644]  ; restored stock operand
00635040  e9 5021eaff      jmp   0x4D7195
```

**`[0x63E12C]` is a frame-delta accumulator; the body runs once per second.**
Time compression is *not* handled here — it is handled as a trial count (§3).

## 2. The tick body — `0x00635049`

```
00635049  60 9c            pushal; pushfd
0063504B  e8 a43d0000      call  0x638DF4             ; common h.sie prologue
00635052  a1 d4e16300      mov   eax, [0x63E1D4]
00635057  a3 -> mov [0x63E1D8], eax                   ; previous := current
0063505D  a1 dc435500      mov   eax, [0x5543DC]
00635062  8b 40 04         mov   eax, [eax+4]
00635065  8b 80 20030000   mov   eax, [eax+0x320]
0063506B  8b 00            mov   eax, [eax]
0063506D  a3 -> mov [0x63E1D4], eax                   ; current := telegraph
00635073  80 3d 13cf6300 ff cmp  byte [0x63CF13], 0xFF ; patch enabled?
0063507A  0f 85 92010000   jne   0x635212
00635080  80 3d 80705400 01 cmp  byte [0x547080], 1   ; unidentified global gate
00635087  0f 85 77010000   jne   0x635204
```

### The telegraph value

`[0x63E1D4]` = `[[[0x5543DC]+4]+0x320]`, a float in `0..1`.
`hsie.ini` names it: **"Speed = Value of machine telegraph"** — the *ordered*
setting, not the boat's actual speed. `[0x63E1D8]` is the previous second's value.

Corroborated by the message at `0x6350A5`: when current becomes `1.0` and previous
was not, it fires string **4894 = "Yes, sir, everything we've got!"** — i.e. the
crew acknowledging Ahead Flank.

### Time-compression gate

```
006350B6  e8 119cedff      call  0x50ECCC             ; Utils.dll!GetTimeComprManager
006350BB  8b 40 08         mov   eax, [eax+8]         ; TC factor (float, 1.0 = 1x)
006350BE  3b 05 d0e16300   cmp   eax, [0x63E1D0]      ; same as last tick?
006350C4  0f 85 3a010000   jne   0x635204             ; changed -> skip this tick
```

`[0x63E1D0]` caches last tick's TC. A tick where the player just changed time
compression is **discarded** rather than mis-scaled.

### Surface / running gate

```
006350CA  d9 05 64c46300   fld   dword [0x63C464]     ; 0.0
006350D0  a1 dc435500      mov   eax, [0x5543DC]
006350D5  d9 40 18         fld   dword [eax+0x18]
006350D8  d8 40 1c         fadd  dword [eax+0x1c]
006350DB  df f1            fcomip st(1)               ; sum vs 0.0
006350DF  0f 82 1f010000   jb    0x635204             ; sum < 0 -> skip
```

Proceeds only when `[0x5543DC+0x18] + [0x5543DC+0x1C] >= 0`. Field identity
**not established** — the obvious reading is a depth/surfaced test (diesels only
run on the surface), but nothing here proves it. `0x63C464` is plain zero-fill,
not a tunable.

### The two diesels

```
006350E5  a1 dc435500      mov   eax, [0x5543DC]
006350EA  8b 40 04         mov   eax, [eax+4]
006350ED  8b 80 0c030000   mov   eax, [eax+0x30C]
006350F3  8b 38            mov   edi, [eax]           ; engine A health word
006350F5  8b 70 04         mov   esi, [eax+4]         ; engine B
006350F8  31 db            xor   ebx, ebx             ; count partially damaged
006350FA  31 ed            xor   ebp, ebp             ; count destroyed
006350FC  ff 37            push  [edi] ; call 0x6357F5
00635103  ff 36            push  [esi] ; call 0x6357F5
```

`0x6357F5` classifies one float: `0.0` → neither counter; `1.0` → `inc ebp`;
anything between → `inc ebx`.

**So the scale is damage, not health: `0.0` = undamaged, `1.0` = destroyed.**
Two engines — port and starboard (`en_menu.txt` 460/461 name them).
`ebx` ∈ {0,1,2} is the count of *partially* damaged diesels and indexes both
constant tables.

## 3. The damage-chance model

```
0063510A  ff 34 9d b8c06300 push [ebx*4 + 0x63C0B8]   ; Speed-Exponent
00635111  ff 35 d4e16300    push [0x63E1D4]           ; telegraph
00635117  e8 8c200000       call 0x6371A8             ; pow()  <- see below
0063511C  d8 0c 9d acc06300 fmul dword [ebx*4+0x63C0AC] ; x Factor
00635123  d8 0d a4c06300    fmul dword [0x63C0A4]     ; x p_dso
00635129  d9 05 a8c06300    fld  dword [0x63C0A8]     ; threshold
0063512F  df f1             fcomip st(1)
00635131  72 07             jb   0x63513A             ; p > threshold -> continue
00635135  e9 ca000000       jmp  0x635204             ; else no damage ever
```

`0x6371A8` marshals two floats to doubles and calls `[0x51D678] - 0xDD50`.
`0x51D678` is the import slot for `MSVCR71.dll!atol`; the fixed negative delta
reaches an **unexported CRT `pow`**. (h.sie's trick for using `pow` without
adding an import.) Likewise `0x417EE0` is `randRange(lo, hi)` built on
`Kernel.dll!?Random32@@YAIXZ` (`0x51D57C`).

**`hsie.ini` states the formula verbatim:**

```
p_ds = p_dso * Factor * Speed^Speed-Exponent     (Speed = machine telegraph)
T50% = ln(0.5) / ln(1 - p_ds)
```

| ini | Address | Meaning | Value |
|---|---|---|---|
| Val9 | `0x63C0A4` | `p_dso` — base probability per second | 2e-4 |
| Val10 | `0x63C0A8` | threshold; below this, never any damage | 1e-6 |
| Val11/12/13 | `0x63C0AC/B0/B4` | Factor for 0 / 1 / 2 damaged diesels | 1.0 / 4.0 / 8.0 |
| Val14/15/16 | `0x63C0B8/BC/C0` | Speed-Exponent for 0 / 1 / 2 damaged | 200 / 20 / 10 |

### Time compression as trial count

```
0063513A  e8 8d9bedff      call 0x50ECCC
0063513F  81 78 08 0000803f cmp dword [eax+8], 0x3F800000  ; TC == 1.0?
00635146  74 0e            je   0x635156                   ; yes -> use p as-is
00635148  ff 70 08         push [eax+8]
0063514B  83 ec 04 / d91c24 push p
0063514E  e8 c2060000      call 0x635818                   ; p := 1-(1-p)^TC
```

`0x635818` computes **`1 - (1 - p)^TC`** — the probability of at least one event
in `TC` independent trials. So one real second at 32× compression is charged as
32 game-seconds of risk. **`p_ds` is therefore a per-*game*-second probability.**

### ⚠ 3a. `0x635818` quantizes `p` onto a float32 lattice

Disassembled 2026-08-15 while re-resolving ticket 04. The helper is 36 bytes,
followed by `int3` padding to `0x635844`:

```
00635818  55 8bec          push ebp; mov ebp, esp
0063581B  50 51 52         push eax; push ecx; push edx
0063581E  ff750c           push  dword [ebp+0xc]      ; TC (exponent)
00635821  d9e8             fld1                       ; 1.0        (80-bit)
00635823  d86508           fsub  dword [ebp+8]        ; 1 - p      (80-bit)
00635826  83ec04           sub   esp, 4
00635829  d91c24           fstp  dword [esp]          ; <-- ROUNDED TO FLOAT32
0063582C  e877190000       call  0x6371a8             ; pow(1-p, TC)
00635831  d9e0             fchs
00635833  d9e8 dec1        fld1; faddp st(1)          ; 1 - result (80-bit)
0063583A  c9 c20800        leave; ret 8
```

`fstp dword [esp]` stores `1 - p` as a **float32**. Immediately below 1.0 the
float32 spacing is exactly **2⁻²⁴ ≈ 5.96e-8**, so `p` is snapped to a multiple of
2⁻²⁴ **whenever time compression is above 1×**:

| `p` supplied | `(1-p)` as float32 | `p` actually used at TC > 1 |
|---|---|---|
| < 2.98e-8 (½ ULP) | `1.0` | **0.0 — no damage, ever** |
| 2.98e-8 … 8.94e-8 | `0.99999994` | 5.9605e-8 |
| 8.94e-8 … 1.49e-7 | `0.99999988` | 1.1921e-07 |

Consequences for anyone retuning `Val9`:

- **There is a silent inert floor at `p_ds = 2.98e-8`.** Below it the mod does
  nothing under time compression, with no error and no in-game symptom. Since
  players spend nearly all time compressed, the mod is effectively dead.
- **The rate is not continuously tunable at these magnitudes** — only 2⁻²⁴ steps
  exist, i.e. 2.15 / 4.29 / 6.44 … events per 10,000 game-hours.
- **The `TC == 1` path disagrees with the `TC > 1` path.** `0x63513F` branches
  past this helper when TC is exactly 1.0, using `p` unrounded. Setting `p_ds` to
  a lattice point exactly (`5.9604645e-8 = 2⁻²⁴`) is the only way to make both
  paths give the same rate.
- **h.sie's shipped values are unaffected**: `p_ds` is ≥ 2e-4 in every case that
  clears the `1e-6` threshold, five orders of magnitude above the lattice. The
  defect is invisible at his tuning and only appears at APE's.

### 3b. APE's replacement for `0x635818`

Ticket 04 **adopts** the escape route. For `p` this small the binomial is
pointless: `1-(1-p)^TC ≈ p × TC` to better than 0.01% even at 4096×. The whole
function is replaced with a clamped multiply — **21 bytes into a 38-byte body**
(`0x635818`–`0x63583D`), `int3`-padded to `0x635844`:

```
00635818  55           push    ebp
00635819  8bec         mov     ebp, esp
0063581B  d94508       fld     dword [ebp+8]      ; p
0063581E  d84d0c       fmul    dword [ebp+0xc]    ; p * TC
00635821  d9e8         fld1                       ; \
00635823  dbf1         fcomi   st(1)              ;  | st0 := min(1.0, p*TC)
00635825  dbc1         fcmovnb st(0), st(1)       ;  |
00635827  ddd9         fstp    st(1)              ; /
00635829  c9           leave
0063582A  c20800       ret     8
```

File offset `0x147818`. Stack discipline matches the original exactly: `stdcall`,
two float args, `ret 8`, net **+1** on the x87 stack. The original's
`push eax/ecx/edx` … `pop` is dropped because nothing here touches GPRs.

The clamp is **required, not defensive.** `p × TC` saturates at 1.0 — damage on
every tick — once `p ≥ 1/TC`:

| TC | runaway above | in events / 10,000 h |
|---|---|---|
| 32× | 3.1e-2 | 1,125,000 |
| 1024× | 9.8e-4 | 35,156 |
| 4096× | **2.4e-4** | 8,789 |

At 4096× that threshold is `2.44e-4` — barely above h.sie's own shipped
`p_dso = 2e-4`, and easily reached by anyone sweeping values in APE's GUI. The
clamp idiom (`fld1; fcomi; fcmovnb; fstp st(1)`) is copied verbatim from h.sie's
own damage clamp at `0x6351D8`.

With this in place the lattice, the `2.98e-8` inert cliff, and the `TC == 1` /
`TC > 1` disagreement all disappear, and `p_dso` becomes continuously tunable
across the whole useful range.

```
00635156  68 0000803f      push 1.0
0063515B  68 00000000      push 0.0
00635160  e8 7b2ddeff      call 0x417EE0                   ; randRange(0.0, 1.0)
00635168  df f1 / ddd8     fcomip st(1); fstp
0063516C  0f 83 92000000   jae  0x635204                   ; roll >= p -> no damage
```

## 4. Applying the damage

Target selection (`edx`), given `ebp` = count already destroyed:

| Case | Target |
|---|---|
| `ebp >= 2` | **exit** — both diesels already destroyed, nothing to do |
| `ebp == 1` | the one that is *not* `1.0` |
| `ebp == 0`, values equal | 50/50 coin flip (`0x6371FD`, a `chance(p)` helper) |
| `ebp == 0`, values differ | the **more** damaged of the two |

Amount:

```
006351B0  68 3d0a173f      push 0.59        ; hi
006351B5  68 0ad7233c      push 0.01        ; lo
006351BA  83 fb 00         cmp  ebx, 0
006351BD  74 0f            je   0x6351CE
006351BF  c7 44 24 04 ...  mov  [esp+4], 1.0   ; hi := 1.0
006351C7  c7 04 24 ...     mov  [esp],   0.3   ; lo := 0.3
006351CE  e8 0d2ddeff      call 0x417EE0       ; randRange(lo, hi)
006351D6  d8 02            fadd dword [edx]    ; += existing damage
006351D8  d9e8/dbf1/dbc1   fld1; fcomi; fcmovnb ; min(sum, 1.0)
006351E0  d9 1a            fstp dword [edx]
```

- **No diesel yet partially damaged (`ebx == 0`): `+= rand(0.01 … 0.59)`.**
- **One or both already partially damaged (`ebx != 0`): `+= rand(0.30 … 1.00)`.**

Clamped to `1.0` (destroyed). These four operands are **inline immediates in
`.pcode`**, *not* entries in `hsie.ini` — the damage *amount* is the one part of
the model h.sie left un-tunable from his config file. They are, however, plain
data operands outside the §0 clobber list, so a static patch to them sticks.

⚠ **Note the selector is a *state*, not an event counter.** `ebx` counts diesels
that are damaged *right now*. Combined with the targeting table above — which
sends the hit to the **more** damaged engine — h.sie's second band only fires
while an earlier injury is still unrepaired, and then concentrates on it.

### 4c. APE's damage-band patch sites

Ticket 04 collapses both bands to a single `rand(0.15 … 0.55)`, on the historian's
constraint that damage must stay repairable (below `1.0`). Rating of what h.sie
ships, for the record:

| Band | Mean | Spread | Behaviour |
|---|---|---|---|
| `0.01 … 0.59` | 0.30 | 59× | Cannot destroy from pristine. But 25% of hits ≤0.16 (barely felt) and 25% ≥0.44. |
| `0.30 … 1.00` | 0.65 | 3× | Additive onto the hurt engine: destroys outright 29% / 43% / 71% of the time from priors of 0.20 / 0.30 / 0.50. |
| **`0.15 … 0.55`** (APE) | **0.35** | **3.7×** | One hit never destroys; two *unrepaired* hits reach 1.0 only 3.1% (h.sie's first band: 4.9%). |

All four immediates are written to the same band, leaving the `cmp ebx, 0` /
`je` / `mov` block intact and harmlessly re-writing values it already holds. The
one-byte alternative at `0x6351BD` (`74`→`eb`, `je`→`jmp`) is behaviourally
identical but changes an instruction; writing operands keeps every site a
verify-context + mutable-field pair, and leaves the escalated pair addressable
should the two bands ever want re-splitting.

| Site | VA | File offset | h.sie | APE default |
|---|---|---|---|---|
| first-hit **hi** | `0x6351B1` | `0x1471B1` | `3d 0a 17 3f` (0.59) | `cd cc 0c 3f` (0.55) |
| first-hit **lo** | `0x6351B6` | `0x1471B6` | `0a d7 23 3c` (0.01) | `9a 99 19 3e` (0.15) |
| escalated **hi** | `0x6351C3` | `0x1471C3` | `00 00 80 3f` (1.00) | `cd cc 0c 3f` (0.55) |
| escalated **lo** | `0x6351CA` | `0x1471CA` | `9a 99 99 3e` (0.30) | `9a 99 19 3e` (0.15) |

⚠ The band is a **GUI-tunable field**, so these are defaults, not constants.
Validation belongs in the tool: `0 < lo < hi < 1`, and `hi > 0.50` makes
two-hit accumulation to `1.0` possible and should be surfaced to the user.

Aftermath:

```
006351E2  e8 e59aedff      call 0x50ECCC
006351E7  c7 40 08 0000803f mov dword [eax+8], 1.0   ; FORCE time compression to 1x
006351EE  81 3a 0000803f   cmp  dword [edx], 1.0
006351F4  74 0e            je   0x635204
006351F6  ...              push 0x131D; call 0x6371D4 ; string 4893
00635204  e8 c39aedff      call 0x50ECCC
00635209  8b 40 08 / a3    mov  [0x63E1D0], [eax+8]  ; cache TC for next tick
```

String **4893 = "If we don't cut power now, we're risking a breakdown!"** — fires
on partial damage only, not on destruction. And the patch **drops the player out
of time compression** whenever it lands a hit.

## 4a. Confirmed against h.sie's own documentation

h.sie's V16 manual, §21 "Diesel Damages Fix" (supplied by the user 2026-08-15),
describes the mechanic in prose and gives a T50 table. **Every documented row
reproduces from the disassembled constants**, which independently validates the
whole model:

| Case | MTS | h.sie doc | Computed from the bytes |
|---|---|---|---|
| a) none damaged | 1.00 | 1 hour | 58 min |
| a) none damaged | ≤0.97 | safe | safe (under the 1e-6 threshold) |
| b) one damaged | 1.00 | 15 min | 14 min |
| b) one damaged | 0.95 | 40 min | 40 min |
| b) one damaged | 0.90 | 2 hours | 2.0 h |
| b) one damaged | 0.80 | 20 hours | 20.9 h |
| b) one damaged | ≤0.70 | safe | safe |
| c) two damaged | 1.00 | 4 min | 4 min ⚠ |
| c) two damaged | 0.90 | 10 min | 10 min ⚠ |
| c) two damaged | 0.70 | 2 hours | 2.1 h ⚠ |
| c) two damaged | 0.50 | 60 hours | 61.6 h ⚠ |
| c) two damaged | ≤0.40 | safe | safe |

The doc also confirms, in h.sie's own words, the two readings this note derived
from the bytes: **"MTS: Machine Telegraph speed. 0.0=stop and 1.0=max"** (so
`[0x63E1D4]` is the ordered telegraph value, not achieved speed), and
**"T50: Timespan that corresponds with a damage probability of 50%"** — i.e. the
documented hours are a *derived output* of the per-second probability, not an
input timer. It also names the acknowledgement string: *"Alles was drin ist…"* /
*"everything we've got!"* — string 4894, exactly as §2 derived.

### ⚠ Val13 drift: the shipped factor is half the documented one

The c) rows reproduce **only with Factor = 16.0** for two damaged diesels. This
install ships `Val13 = 8.0` in both `hsie.ini` and `sh3.exe`, which runs the
two-damaged case at exactly **half** the documented risk:

| MTS | doc (F=16) | this install (F=8) |
|---|---|---|
| 1.00 | 4 min | 7 min |
| 0.90 | 10 min | 21 min |
| 0.70 | 2 hours | 4.3 h |
| 0.50 | 60 hours | 123 h |

Cases a) and b) match the documentation exactly, so this is specific to `Val13`.
Whether h.sie revised it, or OAE softened it, is unresolved — but since
`hsie.ini` wins at runtime (§0), **the ini is the effective source of truth.**

## 4b. Data-side prerequisite: the telegraph values in the Uboat `.cfg`s

h.sie's doc is explicit that the mechanic is keyed to the machine-telegraph
values in each boat's `.cfg`, and that the user must arrange for one slot to
reach 1.0. Surveyed across `OneAlex Extended 1.5.2\data\Submarine\*` on
2026-08-15:

- **Every `HumanPlayable=YES` boat ships `AheadFlank=1.00`** (2A, 2D, 7B, 7C, 9B,
  9C, 9D2, XXI).
- The boats with no `[EngineProperties]` telegraph keys at all (Type XIV, XXIII,
  2B, 7C/41, 7F, 9B/4) are **all `HumanPlayable=NO`** — AI units, irrelevant.
- `AheadFull = 0.85` on every playable boat, comfortably under the 0.97 safe
  line; `AheadStandard = 0.60`, `AheadOneThird` 0.40–0.47, `AheadSlow = 0.20`.

**So on stock OAE data, overload is exactly and only Ahead Flank, and no data
edit is needed.** This matches h.sie's recommended "use your FLANK slot for
overload" layout.

## 5. What the model actually feels like

`T50%` = time to a 50% cumulative chance of the *next* damage event, in
game-time, at a constant telegraph setting:

| Diesels partially damaged | Telegraph 1.00 | 0.99 | 0.98 | 0.95 | 0.90 | 0.80 |
|---|---|---|---|---|---|---|
| **0** | 57.8 min | 7.2 h | 54.7 h | never¹ | never¹ | never¹ |
| **1** | 14.4 min | 17.6 min | 21.6 min | 40.3 min | 118.8 min | 20.9 h |
| **2** | 7.2 min | 8.0 min | 8.8 min | 12.1 min | 20.7 min | 67.2 min |

¹ `p_ds` falls below the `1e-6` threshold, so damage is impossible, not merely rare.

Three things follow:

- **`Speed^200` makes the healthy case a flank-only mechanic.** At telegraph 0.95
  and below with no prior damage, the patch is mathematically inert. It only
  bites at (near-)Ahead Flank.
- **The first hit is the cliff.** Once one diesel is partially damaged the
  exponent drops 200 → 20 and the factor rises 1 → 4; the mechanic becomes active
  across the whole speed range and the damage roll jumps from `0.01–0.59` to
  `0.30–1.00`. A single unlucky flank run cascades.
- **There is no elapsed-running-time term anywhere.** The model is memoryless:
  each game-second is an independent Bernoulli trial over the telegraph setting
  and the current damage count. No counter of engine hours exists in the cave,
  and none of h.sie's 24 parameters is a duration.

## 5a. Levers this gives APE

> ⚠ **Ticket 04 was reopened and re-resolved on 2026-08-15** against a new premise
> (low flat wear rate at any speed, not an overload gate). **Neither lever below is
> used any more** — the feature ships as eight `hsie.ini` keys, with no `ape.act`
> code and no flag writing. Both findings remain true and are kept because they
> are the cheapest known handles on this cave should a future feature want them.
> The tuning that *is* current lives in §3a and in the ticket's answer.

### `[0x63E1D4]` is a free, always-maintained telegraph anchor

The telegraph read at `0x635052`–`0x63506D` happens **before** the enable-flag
test at `0x635073`, and the wrapper's own guards are only "is the singleton
live". Since every h.sie-patched variant carries the identical cave and
trampoline — the variants differ by exactly one byte, the flag (§0 of
[`03-utanker-hull-repair-signature.md`](03-utanker-hull-repair-signature.md)) —
it follows that:

**`[0x0063E1D4]` holds the current machine-telegraph value, refreshed once per
second, at a fixed address, whether or not Diesel Damage is enabled.**

APE can read it directly instead of walking `[0x5543DC]→+4→+0x320→[0]`. Note the
usual ADR-0003 caveat still applies — it is an absolute VA, so it needs a
signature and a uniqueness proof before being trusted as an anchor.

### Flipping the enable flag in live memory is a clean gate

APE can hold `0x63CF13` at `0x00` and write `0xFF` when it wants the mechanic
live. The cave re-reads the flag every second, so the gate takes effect
immediately, and the disabled path is side-effect-free:

- The `jne 0x635212` disabled path lands on `nop; nop; nop` and falls into the
  *next* patch's block (`cmp byte [0x63CF05], 0xFF`, Hydrophone Fix). It does not
  return early or skip unrelated work.
- Telegraph tracking (`[0x63E1D4]`/`[0x63E1D8]`) continues while disabled, since
  it precedes the flag test.
- ⚠ **One-tick warm-up.** `[0x63E1D0]` (the cached time-compression value) is
  written at `0x635204`, which the disabled path skips. So the first tick after
  APE flips the flag on sees a stale `[0x63E1D0]`, fails the
  `cmp eax, [0x63E1D0]` test at `0x6350BE`, and is discarded. Harmless — one lost
  second — but worth knowing so it isn't mistaken for a bug.

### Runtime writes sidestep the `hsie.ini` clobber entirely

`hsie.act` writes its 24 floats once, at load. Anything APE writes to those
addresses **after** that point stands. So a runtime (`ape.act`) implementation
can retune `Val9`–`Val16` freely — it is only the *static, on-disk* patcher that
is blocked by §0.

## 6. Not yet established

- `[0x5543DC + 0x18]` / `[+0x1C]` — the pre-condition sum tested against 0.0.
  Depth/surfaced is the guess, unproven. **⚠ Now load-bearing.** Ticket 04's
  flattened exponent makes the risk speed-independent and continuously live, so a
  wrong reading here means diesels take damage while submerged on electrics —
  where h.sie's `MTS^200` made the same error nearly invisible. Raised as
  umbrella-roadmap ticket 11; also decides whether snorkelling counts as diesel
  running time.
- `[0x547080]` — the byte gate at `0x635080`, compared against `1`.
- The `[0x5543DC]` singleton itself — read here, by the u-tanker mod's block 1,
  and listed as an unknown guard in `01-crush-depth-damage-signature.md` §2.
  **2026-08-17: working hypothesis added** (not proven) — single writer at
  `0x004A2966` looks like a unit-registration site, and it's read directly inside
  both of stock's depth-handling functions. See
  `01-crush-depth-damage-signature.md` §6 for the full offset survey off
  `[[0x5543DC]+4]`, including the `+0x83C` overlap with the `[0x5449B0]+0xD8`
  "god object" candidate.
- Whether the two engine floats at `[[0x5543DC]+4]+0x30C` map to port/starboard
  in a fixed order.
- AOB signatures with wildcards + uniqueness proofs per ADR-0003 — this note
  records absolute VAs from one variant set only.
