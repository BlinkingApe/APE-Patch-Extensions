# RE note: borrowing stock's flooding report for the motor-room warning

Depth-pressure stress ticket 35 (from ticket 34's grilling), 2026-11-25. Static only,
read-only. Nothing here has been proven live yet; the live checks are listed at the end.
The code is `src/ape_act/game_memory.cpp` (reads and writes) and
`src/mods/depth_stress/warning.cpp` (the decision).

**Proven on:** OAE 1.5.2, `C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256
`20360fe8398692172c7cc36e4337f1d5936ee9c9e0eac97e4c3214b2eb132561` (the same build as
note 09), image base `0x400000`.

## How stock speaks a flooding report

The report routine is `0x435B30`, a method of the "Page barracks" menu page (id
`0x67000000`, vtable `0x51FC60`, object size `0x370C`), called from its per-frame update
`0x43A2D0`. For each zone that maps to a compartment, with `L` the flood at
`[[node+0x14]+0x1C]`:

- `L == 0` clears all three latches. `L < 0.35` is band 1, `0.35 <= L < 0.60` band 2,
  `L >= 0.60` band 3. Band 2 and 3 clear the lower bands' latches on every pass.
- A band speaks when its latch byte is 0: it asks the string table for id 592, 593 or
  594 (`push 0x250; mov ecx,0x566058; call 0x4CA800` @`0x436C25`), formats it into the
  page's buffer at `+0x35DC` with `sprintf(buf, text)` (`[0x51D70C]` is MSVCR71
  `sprintf`: the text is a format string, so **no `%`**), sets the latch, and queues the
  message. There is no "came from below" check: clearing the latch with `L` mid-band makes
  it speak on the next pass.
- `0x4CA800` is a bare array lookup: `[[table+0x130] + id*4]`, bounds-checked against
  `[table+0x138]`. The strings are `char*` in a static array, so one aligned 4-byte write
  swaps what a band says.
- The latch bytes are `page + 0x32B4 + 12*(band-1) + compartment`. The compartment is
  the byte the zone table `0x566898` gives the zone; the motor room is compartment 4
  (zone 30), so its latches are `+0x32B8`, `+0x32C4`, `+0x32D0`.
- The voice is chosen by number (station 0, message 5, 4 or 3 for bands 1, 2, 3 from
  `0x4EF9E0`), never by the text. OAE's `Message_CH.cfg` has `[Msg4]` and `[Msg5]`
  commented out, so bands 1 and 2 should be silent on OAE (inferred, not heard).
- The latch is set *before* a crew gate (`0x4E33F0(0)`). If the gate fails, the latch is
  set and nothing shows, so "latch set again" does not prove the message appeared.

## Signatures

The operand's four bytes are the only wildcard in the address. Each pattern matched
exactly once across all executable sections of the build above and recovered the
expected address.

| Root | Signature | Root at | Instruction (VA) |
|---|---|---|---|
| Report root `0x005449B0` | `89 35 ?? ?? ?? ?? 89 1D ?? ?? ?? ?? 68 ?? ?? ?? ?? 68 ?? ?? ?? ?? B9 ?? ?? ?? ?? 89 9E D0 00 00 00` | +2 | `mov [0x5449B0],esi; mov [0x5540A0],ebx; push; push; mov ecx; mov [esi+0xD0],ebx` @`0x00408DA6`, the root's constructor |
| String table object `0x00566058` | `68 50 02 00 00 B9 ?? ?? ?? ?? E8` | +6 | `push 0x250; mov ecx,0x566058; call 0x4CA800` @`0x00436C25` |

Chains: the report page is `[[0x5449B0] + 0x100]` (the root caches
`FindByID(0x67000000)` there, `0x4091D4`). The string pointer for band `b` is the slot
`[0x566058+0x130] + (591+b)*4`, with id `< [0x566058+0x138]`.

## What APE does with them

1. Condition and band: `warning.cpp` (unit tested).
2. Swap: read the slot's current pointer, write the address of APE's text into it, then
   clear the latch byte. A failed latch write puts the pointer straight back.
3. Restore: on the first poll that reads the latch set, or after 10 real seconds, or when
   the loop ends. It writes the saved pointer back only while the slot still holds APE's
   text.

## Accepted costs

- **The string slot is global.** While APE's pointer is in the table, any report in that
  band, from any compartment, shows APE's text. The window is one or two report passes
  (ticket 34 accepted it).
- **The slot address is cached for the restore.** The write-back happens only while the
  slot still holds APE's text, so a changed or moved table is never written over.

## Still to check live

- `[[0x5449B0]+0x100]` is non-null on patrol, survives a save and load, and `+0x32B8`
  toggles as compartment 4 floods.
- The swapped text appears in the message log within about a game-second, once.
- OAE is silent for band 1 (otherwise the chief says "We have flooding, sir" over APE's
  text, which the ticket accepts).
- The string array doesn't move on a save and load while a swap is in flight (the cached
  slot is then still the right one).
- What `page+0x8C` and the crew gate `0x4E33F0(0)` mean in normal play.
