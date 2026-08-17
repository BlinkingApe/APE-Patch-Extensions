# RE: resolve the live values APE reads/writes

Status: needs-info — kept for its static-pass write-up only; its dynamic hunts are
superseded by tickets 08/09 (amended 2026-08-17, ticket 13)

## ⚠ Amendment (2026-08-17, ticket 13 — retiring the superseded model)

This ticket predates the tier-model pivot and describes the abandoned model's live
values. Corrections:

- **CDT (crush depth) is dead.** The tier model never reads crush depth (map, Out of
  scope). Drop it from any remaining acceptance criteria below; do not hunt for it.
- **The `CrashSpeed` data-file task is dead too**, for the same reason ticket `07`
  (JSGME + the `CrashSpeed=0` edit) was deleted outright: the new model hands tier 3
  back to stock untouched, so there is no neutralization edit to author.
- **The player-boat and current-depth pointer-chain hunts are still live**, but their
  home is now tickets `08` (depth + mission clock) and `09` (boat/hull-HP write) on
  the depth-pressure-stress map, not here. A 2026-08-17 static pass found a
  candidate root (`0x5543DC`) for both — see
  `docs/re-notes/01-crush-depth-damage-signature.md` §6.
- **The mission clock hunt was never in this ticket's original four** (it predates
  that mechanic too) — it's ticket `08` only.

What's still valid and worth keeping: the static-pass write-up (§1 anchors, §5
method) in `docs/re-notes/01-crush-depth-damage-signature.md`, which tickets `08`
and `09` both cite directly.

## Parent

.scratch/depth-pressure-stress/PRD.md

## What to build (original — superseded by the amendment above)

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

## Acceptance criteria (superseded — kept for history; see amendment above for what's live)

- [x] ~~Hull HP: player boat object resolved...~~ → moved to ticket `09`.
- [x] ~~Current depth: `movementObj` resolved...~~ → moved to ticket `08`.
- [x] ~~CDT: crush-depth value located...~~ → **dead**, tier model never reads CDT.
- [x] ~~Mission clock: located...~~ → moved to ticket `08`.
- [x] ~~Each anchor's AOB signature...~~ → still the right bar, now applied by `08`/`09`.
- [x] ~~Per-unit `CrashSpeed` data-file location...~~ → **dead**, no neutralization edit
      in the tier model (same reasoning as ticket `07`'s deletion).

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

Still open when this ticket was live — all **dynamic** (needs the live game + a
human diving the boat), per RE note §4. **Superseded 2026-08-17 (ticket 13):**
- ~~#1 boat pointer chain~~ → ticket `09`.
- ~~#2 movementObj pointer chain~~ → ticket `08`.
- ~~#3 CDT via the differential depth access-trace~~ → dead, never read.
- ~~#4 mission clock~~ → ticket `08`.
- ~~`CrashSpeed` data-file location~~ → dead, no neutralization edit needed.

**Why needs-info (historical):** I couldn't drive the live-game dynamic steps
without a human at the controls. That's still true of tickets `08`/`09` — this
ticket itself has nothing further to resume.
