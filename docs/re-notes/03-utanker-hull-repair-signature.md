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
20 flags observed (`0x63CF00`–`0x63CF13`), each with at least one code gate.

| Flag VA | File offset | Patch |
|---|---|---|
| `0x0063CF0A` | `0x0014EF0A` | Move WO (left on for compatibility) |
| `0x0063CF0C` | `0x0014EF0C` | **U-Tanker** |
| `0x0063CF13` | `0x0014EF13` | Diesel Damage |

To identify any other patch's flag: build a variant with only that patch enabled
and diff against `sh3_vanilla.exe`. The one differing byte is the flag.

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
  objects, not yet reconciled.

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
- Whether `0x63CF0C` gates anything beyond `0x63374F`; a **separate** flag
  `0x63CF05` (gates `0x63353D`, `0x633575`) also reads the docking variable
  `[0x63E164]`/`[0x63E15C]` — so "the U-Tanker mod" may span more than one flag.
- Hash/verification against an actual OAE install binary (`4B6BEE20` or current).
- AOB signatures with wildcards + uniqueness proof, per ADR-0003 — this note
  records absolute VAs from one variant set only, which is **not** yet
  ADR-0003-compliant.
