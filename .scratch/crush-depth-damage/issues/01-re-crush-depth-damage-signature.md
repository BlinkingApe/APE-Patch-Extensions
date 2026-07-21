# RE: resolve the live values APE reads/writes

Status: ready-for-human

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

Documented, versioned AOB-anchored pointer chains for the live values APE's loop
depends on, produced via Cheat Engine (dynamic discovery) and x32dbg (disassembly
confirmation), per ADR-0003. **This replaces the abandoned "find the crush-depth
damage function" hunt** — APE runs alongside and reads/writes memory, it does not
hook a function (ADR-0001). See `docs/re-notes/01-crush-depth-damage-signature.md`
for the START-HERE recipe and the retained closed-context on why no function is
hooked.

Four live values, all anchored in stable stock sh3.exe:

1. **Player boat object → hull HP** (`[boat + 0xB8]`) — offset known; need the AOB
   anchor + chain to `boat` (getter at `004E1C99`, ECX = boat).
2. **Current depth** (`[movementObj + 0x54]`) — offset known; need the AOB anchor +
   chain to `movementObj`.
3. **CDT (crush depth)** — location unknown; found via the differential depth
   access-trace (we need the value's address/offset, read live, never hardcoded).
4. **Mission clock** — drives game-second `dt`; record address, chain, and
   format/resolution.

Plus a **data-file** task (not a debugger task): identify which OAE data file holds
the per-unit `CrashSpeed`, so issue 07 can ship a `CrashSpeed`=0 edit per playable
u-boat class.

## Acceptance criteria

- [ ] Hull HP: player boat object resolved via an AOB-anchored chain in stock
      sh3.exe; `[boat + 0xB8]` confirmed to read/write the player's hull.
- [ ] Current depth: `movementObj` resolved via an AOB-anchored chain;
      `[movementObj + 0x54]` confirmed against the depth gauge.
- [ ] CDT: crush-depth value located via the differential access-trace; its
      address/offset chain recorded, and confirmed to track the live per-boat value
      (verify it changes if a crush-depth-randomizing mod is active).
- [ ] Mission clock: located and confirmed to freeze on pause and scale with time
      compression; format/resolution documented.
- [ ] Each anchor's AOB signature (with wildcards + uniqueness check), extracted
      static operand, offset chain, and volatile-byte notes recorded in
      `docs/re-notes/` against the OAE build verified.
- [ ] Per-unit `CrashSpeed` data-file location identified for the playable u-boat
      classes (hand-off to issue 07).

## Blocked by

None — can start immediately. Needs the installed OAE sh3.exe + Cheat Engine +
x32dbg (all confirmed present on this machine).
