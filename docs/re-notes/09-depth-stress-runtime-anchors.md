# RE note: the depth-stress runtime anchors (boat, depth, clock)

Depth-pressure stress ticket 24, 2026-11-25. Static only. The PRD's three roots had
proven static addresses but no recorded signature: the boat root `0x00554A88`, the
depth root `0x006307BC` (`"sh3.exe"+0x2307BC`), and h.sie's clock mirror `0x0063FF80`.
This note gives each one an AOB signature, the instruction it comes from, and the chain
`ape.act` follows from it. The code is `src/ape_act/game_memory.cpp`.

**Proven on:** OAE 1.5.2, `C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256
`20360fe8398692172c7cc36e4337f1d5936ee9c9e0eac97e4c3214b2eb132561` (the build of
`08-oae-port-of-depth-leads.md`), hashed at the start of the session.

**Method.** Every instruction with an absolute memory operand (or a `push imm32`) naming
each root was listed with `pefile` and `capstone`, across all three executable sections
(`.text`, `.pcode`, and h.sie's second `.text` at `0x640000`). For each candidate, the
bytes from the instruction onwards were grown with the root's four bytes wildcarded
until the pattern matched once across all executable sections. A few bytes were then
added as margin. `ape.act` runs the same scan in `src/core/aob.hpp`, and a throwaway
x86 harness ran that header over the file and got the same single hits and roots.
Scripts: session scratchpad, not kept.

## Signatures

The root's four bytes are always the only wildcard. They are wildcarded so the
signature survives a relink that moves the variable. The address is then read from
the match, at the offset given.

| Root | Signature | Root at | Instruction (VA on `20360fe8`) | Stock? |
|---|---|---|---|---|
| Boat root `0x00554A88` | `89 0D ?? ?? ?? ?? 6A 58 89 5E 10 FF 15` | +2 | `mov [0x554A88],ecx; push 0x58; mov [esi+0x10],ebx; call [iat]` @`0x004BA972` | yes |
| Depth root `0x006307BC` | `8B 3D ?? ?? ?? ?? 85 FF 74 15 8D 9B 00 00 00 00 8B 07` | +2 | `mov edi,[0x6307BC]; test edi,edi; je +0x15; lea ebx,[ebx+0]; mov eax,[edi]` @`0x0050EF70` | yes |
| Clock `0x0063FF80` | `A3 ?? ?? ?? ?? 89 86 50 01 00 00 E9` | +1 | `mov [0x63FF80],eax; mov [esi+0x150],eax; jmp 0x4C211B` @`0x0051CFC7` | **no, h.sie's cave** |

- **Boat root.** The PRD's note that "the only recorded reference to `0x554A88` is
  h.sie's cave at `0x00633633`" is out of date: stock `.text` names it about 150 times,
  almost all reads. The anchor chosen is stock's **store** of the root (a writer, so
  it means "this is the variable", not "something reads it"). The `call [iat]`'s
  operand is left out of the pattern.
- **Depth root.** Eleven stock instructions name it. This one walks the list it heads
  (`mov edi,[edi+4]` follows a few bytes later), which is consistent with the chain's
  first `+0x4` link.
- **Clock.** Nothing in stock code names `0x0063FF80`. The one reference is h.sie's cave
  that replaces stock's clock store at `0x004C2115` (note 08 §3). The signature takes
  in stock's `mov [esi+0x150],eax`, so it matches only where the mirror is written from
  the same register as stock's clock. A bundle without that cave fails this anchor,
  so every poll is skipped (fail inert), as the PRD asks.

## Uniqueness, and other builds

Each signature matched **exactly once across all executable sections**, and recovered
the expected root, in all four binaries on hand:

| Binary | sha256 (short) | Boat | Depth | Clock |
|---|---|---|---|---|
| OAE 1.5.2 (target) | `20360fe8` | 1 → `0x554A88` | 1 → `0x6307BC` | 1 → `0x63FF80` |
| OAE 2.0 (TEST_2) | `0f1176b6` | 1, same | 1, same | 1, same |
| GWC-KC 2.5 | `dc8bd5dc` | 1, same | 1, same | 1, same |
| `data/exe_with_hsie_patches/sh3_vanilla.exe` | `ba59e4c8` | 1, same | 1, same | 1, same |

The KC and OAE 2.0 rows only show the scan resolves. Nothing has been proven live
there (PRD, Out of Scope).

## Chains (from the PRD and note 08, unchanged)

| Value | Chain from the root | Type |
|---|---|---|
| Gauge depth D | `[[[depth root]+0x4]+0x4]+0x54` | float, m |
| Mission clock | `[clock]` | int32, game-s |
| Boat | `[[[[[boat root]+0xC4]+0x2240]+0x24]+0x4C]`, the pointer itself being the identity | pointer |
| Hull, max hull | boat `+0xB8`, `+0xB4` | float, HP |
| Rated crush field CD | boat `+0xC0` | float, tens of m |
| Crush speed CS | boat `+0xC4` | float, HP/s |

The hull multiplier writes boat `+0xB8`, at the boat address read in the same poll.

## Failure behaviour

- **An anchor that doesn't resolve** (no match, or more than one) is logged once at loop
  start, as `anchor '<name>' unresolved (<why>)`. Every poll then skips with "an anchor
  did not resolve". The scan runs once, on `ape.act`'s own thread, never under the
  loader lock.
- **A null link or a failed read** (a pointer below `0x10000`, or `ReadProcessMemory`
  failing) skips the poll with "depth chain unresolved", "clock unreadable", "boat chain
  unresolved" or "boat fields unreadable".
- **A failed plausibility gate** skips with the gate's reason. These are the step's
  `implausible()` checks, covered by `tests/depth_stress_test.cpp`.

Skips are logged when the reason changes. A reason that hasn't changed is logged again
at most every 30 s, with a count.

## Electric-engine anchors (ticket 26, 2026-11-25)

Static only; the in-play check is ticket 27's test 3. The two AOBs recorded in
`08-oae-port-of-depth-leads.md` §§1-2 are now scanned by `ape.act`, with the operand
bytes wildcarded as above. Each matched exactly once, raw-file search, in OAE 1.5.2
(`20360fe8`) and `sh3_vanilla.exe`, recovering the same operand.

| Root | Signature | Value at +2 |
|---|---|---|
| Engine root | `89 35 ?? ?? ?? ?? 57 8D 7E 10` | `0x005543DC` |
| Electric block offset | `8B 80 ?? ?? ?? ?? 8D 77 FC` | `0x37C` |

Chain: `p = [[[root]+0x4]+off]`, then port = `[p+0]` and starboard = `[p+4]`, each the
address of that engine's damage float (0 healthy, 1 destroyed). A sample needs both
floats in [0, 1]; the offset must be in (0, 0x1000]. Any failure skips the poll with
"engine chain unresolved". The writer stores the step's value at the address read in the
same poll.
