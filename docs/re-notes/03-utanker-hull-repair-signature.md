# U-Tanker mod / hull-repair patch site

Verified against the `data/exe_with_hsie_patches/` variant set (see §0 — **not**
build `4B6BEE20`; re-verify before use against an install binary). Found by static
diffing, not live debugging — method in
[`02-hsie-patch-identification-method.md`](02-hsie-patch-identification-method.md),
whose `hsie.act` premise this note corrects.

## 0. Binaries this was verified against

Three variants supplied 2026-08-15, each 1,417,216 bytes — same size as build
`4B6BEE20` but none matching its hash, so treat as a sibling build.

| Variant | SHA-256 | Patches enabled |
|---|---|---|
| `sh3_vanilla.exe` | `BA59E4C8…CACB62E` | Move WO only |
| `sh3_UTanker.exe` | `B640184D…551A61A0` | Move WO + U-Tanker |
| `sh3_Diesel_Damage.exe` | `AF208C8D…6D70FBC6` | Move WO + Diesel Damage |

Each patched variant differs from vanilla by **exactly one byte** — its enable flag.

## 1. h.sie's sections inside sh3.exe

The patches do **not** live in `hsie.act`. h.sie appended four sections to
`sh3.exe` itself:

| Section | VA | Raw | Role |
|---|---|---|---|
| `.pcode` | `0x633000` | `0x145000` | patch code caves |
| `.pconst` | `0x63C000` | `0x14E000` | constants, strings, **the flag table** |
| `.pdata` | `0x63E000` | `0x150000` | patch variables (writable) |
| `.text` #2 | `0x640000` | `0x152000` | additional patch code |

## 2. The flag table

At **VA `0x0063CF00`** (file `0x0014EF00`), one byte per patch,
`0xFF` = enabled, `0x00` = disabled. Tested as `cmp byte ptr [0x63CFxx], 0xff`.
**22** flags, not the 20 originally observed (`0x63CF00`–`0x63CF15`).

### The full table is documented — no diffing required

**Corrected 2026-08-15.** The variant-diff method below still works, but is no
longer necessary: Stiebler's `HsieOptionsSelector` ships a plain-text data file,
`HSIEContents.txt`, in the OAE install root, which **declares the flag table's
file offset and names every patch in table order**. Its header reads:

```
:1371904:flags          // 0x14EF00 — where the flags are (00 or FF)
:1371136:V 1.6E1        // 0x14EC00 — h.sie version string, used as a version test
:1368180:slider values. // 0x14E074 — first numeric parameter; each next is +4
```

Entry *n* is at VA `0x63CF00 + (n-1)`. Three entries derived independently by
diffing (`0x63CF0A`, `0x63CF0C`, `0x63CF13`) all match this ordering exactly,
which is what validates the table:

| # | Flag VA | File offset | Patch | Install `51d14ca3` |
|---|---|---|---|---|
| 1 | `0x0063CF00` | `0x0014EF00` | Realistic Repair Times | ON |
| 2 | `0x0063CF01` | `0x0014EF01` | WO Range Inaccuracy | ON |
| 3 | `0x0063CF02` | `0x0014EF02` | WP Range Inaccuracy | ON |
| 4 | `0x0063CF03` | `0x0014EF03` | Snorkelling Fix | off |
| 5 | `0x0063CF04` | `0x0014EF04` | Periscopes Fix | off |
| 6 | `0x0063CF05` | `0x0014EF05` | Hydrophone Fix | off |
| 7 | `0x0063CF06` | `0x0014EF06` | Fog Warning | ON |
| 8 | `0x0063CF07` | `0x0014EF07` | CrashDive Blues Bugfix | ON |
| 9 | `0x0063CF08` | `0x0014EF08` | External Torpedo Reload | ON |
| 10 | `0x0063CF09` | `0x0014EF09` | Internal Torpedo Reload | ON |
| 11 | `0x0063CF0A` | `0x0014EF0A` | Move 1WO to bridge | ON |
| 12 | `0x0063CF0B` | `0x0014EF0B` | Oxygen supply Fix | ON |
| 13 | `0x0063CF0C` | `0x0014EF0C` | **U-Tanker Fix** | ON |
| 14 | `0x0063CF0D` | `0x0014EF0D` | BDU Messages | ON |
| 15 | `0x0063CF0E` | `0x0014EF0E` | War News Fix | ON |
| 16 | `0x0063CF0F` | `0x0014EF0F` | Radar Bugfix | ON |
| 17 | `0x0063CF10` | `0x0014EF10` | Wolfpack Mod | ON |
| 18 | `0x0063CF11` | `0x0014EF11` | Wolfpk less contact reports | ON |
| 19 | `0x0063CF12` | `0x0014EF12` | Torpedo Failure Fix | ON |
| 20 | `0x0063CF13` | `0x0014EF13` | **Diesel Damage Fix** | **off** |
| 21 | `0x0063CF14` | `0x0014EF14` | Night Vision Fix | off |
| 22 | `0x0063CF15` | `0x0014EF15` | Bad Weather Fix | off |

The right-hand column is the live state of `C:\games\OneAlex Extended 1.5.2\sh3.exe`
(sha256 `51d14ca3…`, 1,417,216 bytes), read 2026-08-15. **Note Diesel Damage is
disabled in that install** — relevant to the diesel-damage effort, which targets
a patch the user is not currently running.

Two further facts from the same source:

- **h.sie's numeric parameters** ("slider values") are a float array at VA
  `0x63C074` (file `0x14E074`), 4 bytes apiece. Live values read
  `[3.5, 1.0, 0.25, 0.4, 0.15, 0.1, 0.03, 0.25, 0.1, 0.02, 0.98, 0.0]`. These do
  **not** obviously correspond to `hsie.ini`'s `Val1=60.0`, `Val2=0.5` — whether
  `hsie.ini` is applied at patch time, read at runtime, or stale here is
  **unresolved** and worth a look before trusting either source.
- **A version string** `v 1.6E1` sits at VA `0x63CC00` (file `0x14EC00`), which
  `HSIEContents.txt` states is "used as a test for the correct version".

### Fallback method (still valid)

To identify a flag without the data file: build a variant with only that patch
enabled and diff against `sh3_vanilla.exe`. The one differing byte is the flag.

### What writes these bytes

`Support/Stiebler4C_V16B1/HsieOptionsSelector_V16C.exe`. Its readme is explicit
that it "alters the ON-OFF switches in the patched SH3.exe" — **flag bytes only**.
It does not rewrite caves or constants, so edits to patch-cave operands survive a
subsequent `HsieOptionsSelector` run.

## 3. Docking detection — sets the gate variable

Cave at **`0x00633743`**, hooked from stock code, returning via
`call 0x440020; jmp 0x440189`.

```
00633745  c7 05 68 e1 63 00 00 00 00 00  mov dword [0x63E168], 0   ; default: not docked
0063374F  80 3d 0c cf 63 00 ff           cmp byte [0x63CF0C], 0xFF ; U-Tanker enabled?
00633756  75 3d                          jne  0x633795
00633758  83 fd 02                       cmp  ebp, 2
0063375D  81 ff 00 00 01 00              cmp  edi, 0x10000
00633765  8d 35 0a cc 63 00              lea  esi, [0x63CC0A]      ; "Dock at: U-"
0063376B  b9 0b 00 00 00                 mov  ecx, 11
00633771  f3 a6                          repe cmpsb
00633779  8d 35 18 cc 63 00              lea  esi, [0x63CC18]      ; "Anlegen in: U-"
0063377F  b9 0e 00 00 00                 mov  ecx, 14
0063378B  c7 05 68 e1 63 00 01 00 00 00  mov dword [0x63E168], 1   ; docked at a U-tanker
```

**`[0x0063E168]` is the "docked at a U-tanker" boolean** — the gate every resupply
action below tests. Localised by matching the mission-order string, EN and DE.

## 4. The resupply cave — three independent actions

Hooked by **`jmp 0x6335A1` planted at `0x00455CB4`** (stock `.text`), returning via
`push 0x521de8; jmp 0x455CB9`.

```
006335A1  60 9c                          pushal; pushfd
006335A3  83 3d 68 e1 63 00 01           cmp dword [0x63E168], 1   ; docked?
006335AA  0f 85 79 01 00 00              jne  0x633729             ; -> clear + return
                                         ; guards: [0x5F603C] in (0,3); >=14 units
                                         ; since [0x5F635C]; [0x63E118]!=3; [0x63E104]<=10
```

### Block 1 — second scalar resource (fuel, **inferred not proven**)

```
006335FA  8b 1d dc 43 55 00              mov  ebx, [0x5543DC]
00633600  8b 5b 04                       mov  ebx, [ebx+4]
00633603  d9 05 10 c4 63 00              fld  dword [0x63C410]     ; 0.30
00633609  dc 8b 58 03 00 00              fmul qword [ebx+0x358]    ; x max
0063360F  dc 83 60 03 00 00              fadd qword [ebx+0x360]    ; + current
00633615  dd 93 60 03 00 00              fst  qword [ebx+0x360]    ; store
0063361B  dc 9b 58 03 00 00              fcomp qword [ebx+0x358]   ; clamp to max
```

`+= 30% of max`, doubles. Field identity unconfirmed — fuel is the obvious
candidate given the mod's purpose, but nothing here proves it.

### Block 2 — HULL REPAIR ← the ticket-03 target

```
00633633  8b 1d 88 4a 55 00              mov  ebx, [0x554A88]
00633639  8b 9b c4 00 00 00              mov  ebx, [ebx+0xC4]
0063363F  8b 9b 40 22 00 00              mov  ebx, [ebx+0x2240]
00633645  8b 5b 24                       mov  ebx, [ebx+0x24]
00633648  8b 5b 4c                       mov  ebx, [ebx+0x4C]      ; ebx = player boat
0063364B  68 10 00 00 00                 push 0x10
00633650  e8 9f 3a 00 00                 call 0x6370F4             ; rand(0..15)
00633655  83 c0 05                       add  eax, 5               ; -> 5..20
00633659  db 04 24                       fild dword [esp]
0063365D  d8 0d 04 c4 63 00              fmul dword [0x63C404]     ; x 0.01
00633663  d8 8b b4 00 00 00              fmul dword [ebx+0xB4]     ; x MAX hull
00633669  d8 83 b8 00 00 00              fadd dword [ebx+0xB8]     ; + CURRENT hull
0063366F  d9 93 b8 00 00 00              fst  dword [ebx+0xB8]     ; STORE hull HP
00633675  d8 9b b4 00 00 00              fcomp dword [ebx+0xB4]    ; clamp to max
00633681  8b 83 b4 00 00 00              mov  eax, [ebx+0xB4]
00633687  89 83 b8 00 00 00              mov  [ebx+0xB8], eax
```

**Model: `hull += (rand(0..15) + 5) × 0.01 × maxHull`, clamped — a random
5–20% of max hull, per docking.**

- `[boat+0xB8]` (float) = hull HP, `[boat+0xB4]` = max hull. The `+0xB8` offset
  **independently corroborates** [`01-crush-depth-damage-signature.md`](01-crush-depth-damage-signature.md)
  §1, which derived the same field from the HUD mirror at `0x00566A28`.
- ⚠ The rate constant `0.01` at `0x63C404` is a **generic shared constant**, not
  private to this patch — do not repoint or overwrite it. Edit the roll operands
  (`push 0x10`, `add eax,5`) or skip the block instead.
- Boat pointer chain here (`[0x554A88]→+0xC4→+0x2240→+0x24→+0x4C`) differs from
  block 1's (`[0x5543DC]→+4`) and from the chains in
  `01-crush-depth-damage-signature.md` §4 — three routes to player-adjacent
  objects, not yet reconciled. **2026-08-17: this note's own chain turned out to be
  the answer depth-pressure-stress ticket `09` was hunting for** — `[ebx+0xB4]`/
  `[ebx+0xB8]` here are the same max-hull/hull-HP fields that ticket targets, and
  this chain is now cross-referenced and re-verified in
  `01-crush-depth-damage-signature.md` §7. Still not reconciled against the other
  two routes, but no longer needs to be — `09` only needs one working chain to the
  boat, and this is a proven one.

### Block 3 — torpedo reload

```
0063368D  31 ff                          xor  edi, edi              ; loaded count
0063368F  8b 15 c4 46 55 00              mov  edx, [0x5546C4]
00633695  8b 12 / 8b 52 2c / 8b 92 b8..  mov  edx, [edx]→[+0x2C]→[+0xB8] ; slot array
006336A5  8b 0d 00 e0 63 00              mov  ecx, [0x63E000]
006336B6  68 04 00 00 00                 push 4
006336BB  e8 34 3a 00 00                 call 0x6370F4              ; rand(0..3)
006336C0  40                             inc  eax                   ; -> 1..4
006336CC  83 3c 9a 00                    cmp  dword [edx+ebx*4], 0  ; empty slot?
006336D2  89 0c 9a                       mov  [edx+ebx*4], ecx      ; fill it
```

Fills up to 4 empty slots with `rand(0..3)+1` torpedoes.

### Exit

```
006336E4..006336F2                       report count, message 0x12DE
006336F9 / 00633709 / 00633719           refusal messages 0x12DF / 0x12E0 / 0x12E1
00633729  c7 05 68 e1 63 00 00 00 00 00  mov dword [0x63E168], 0    ; clear gate
00633733  9d 61                          popfd; popal
00633735  68 e8 1d 52 00                 push 0x521DE8              ; restored operand
0063373A  e9 7a 25 e2 ff                 jmp  0x455CB9              ; back to stock
```

## 5. Disabling hull repair only

The boundary ticket 03 asks for is exact: **blocks 1 and 3 and every message are
untouched**; only block 2 changes. Two candidate edits, both single-site:

- **Skip the block** — branch `0x633633 → 0x63368D`. Hull HP is never written, so
  there is no interaction with APE's crush-depth model at all.
- **Narrow the roll** — edit `push 0x10` (roll width) and `add eax, 5` (floor) to
  set a min/max percentage. `0` for both yields `+= 0` with the code path intact.

Per the ticket-03 resolution the second is the shipping shape, driven by a GUI
patcher rather than `ape.cfg`.

## 6. Not yet established

- Block 1's field identity (fuel?) — needs a live read or a second xref route.
- ~~Whether `0x63CF0C` gates anything beyond `0x63374F`; a **separate** flag
  `0x63CF05` (gates `0x63353D`, `0x633575`) also reads the docking variable
  `[0x63E164]`/`[0x63E15C]` — so "the U-Tanker mod" may span more than one
  flag.~~ **Resolved 2026-08-15 by the flag table in §2**: `0x63CF05` is entry
  #6, **Hydrophone Fix** — a different patch that merely *consumes* the same
  docking state (plausibly to alter hydrophone behaviour in port). The U-Tanker
  mod is `0x63CF0C` alone. Note Hydrophone Fix is `0x00`/off in the live install,
  so those two caves are inert there.
- Hash/verification against an actual OAE install binary (`4B6BEE20` or current).
- AOB signatures with wildcards + uniqueness proof, per ADR-0003 — this note
  records absolute VAs from one variant set only, which is **not** yet
  ADR-0003-compliant.
