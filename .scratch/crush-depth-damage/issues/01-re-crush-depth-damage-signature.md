# RE: resolve the live values APE reads/writes

Status: needs-info (static half DONE; dynamic half needs a live-game session — see Comments)

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

## Comments

**STATIC HALF COMPLETE (2026-07-21, build `4B6BEE20`).** SHA-256 of the install at
`C:\games\OneAlex Extended 1.5.2_pre-release\sh3.exe` re-confirmed to match the
registered build. Binary Ninja here is GUI/personal (no headless API), so the
static pass was scripted with Python `pefile`+`capstone` (persisted at
`tools/re-static/`, method in RE note §5). Full detail in
`docs/re-notes/01-crush-depth-damage-signature.md` §1/§2/§5.

Done:
- **#1 hull anchor** — AOB `8B 81 B8 00 00 00 A3 28 6A 56 00` @`004E1C99`, **unique**
  (1 match). Confirms `[boat+0xB8]` = hull HP. Bonus: `[0x00566A28]` is a static,
  always-current, read-only **HP-value mirror** (write it → engine overwrites; use
  for reads/plausibility only).
- **#2 depth anchor** — AOB `D9 44 24 14 D8 59 54 DF E0 F6 C4 41 0F 85 ?? ?? ?? ??`
  @`00513FFD`, **unique** (1 match). Confirms `[movementObj+0x54]` = current depth.
- Getter fn `004E1C00` (1 caller `004F9559`) and depth-setter fn `00513FC0` (1 caller
  `0051457A`) both reach the object via a `this`/local — **not** a static-rooted
  chain. Candidate static singleton roots recorded (`0x5449B0`/`0x55408C`/`0x5543DC`);
  `[[0x5449B0]+0xD8]` is a god/manager object, not the player object.

Still open — all **dynamic** (needs the live game + a human diving the boat), per
RE note §4:
- [ ] #1 boat pointer chain (for the HP *write*) — CE pointer-scan / find-what-accesses.
- [ ] #2 movementObj pointer chain.
- [ ] #3 CDT via the differential depth access-trace.
- [ ] #4 mission clock (address, chain, format/resolution; pause/time-compression).
- [ ] `CrashSpeed` data-file location (hand-off to 07).

**Why needs-info:** I can't drive the live-game dynamic steps (they need SH3 running
with a human at the controls to dive below crush depth and watch memory). Ready to
resume the write-up the moment you run a CE session with those results.
