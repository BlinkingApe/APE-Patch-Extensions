# 34: Warn the player before the motors take flooding damage

Type: grilling
Status: resolved
Blocked by: none

## Parent

Stoss's second recommendation (via the owner, 2026-11-25): "at 15 % flooding, a warning
message to pump or shut down the motors; at 20 % damage, rapid catastrophic damage."

The second half is done: the flooding threshold default is now **20 %**, and motor damage
stays at 10 % per game-second, which the owner accepts as rapid and catastrophic. Both
defaults are no longer provisional (ADR-0012, PRD, README, patcher, `ape.act`).

This ticket is the first half: an in-game message when the electric engine room reaches
15 %.

## Constraint

ADR-0001: APE never hooks or calls a stock function. It only reads and writes memory from
its own thread. Ticket 28 already found that custom report wording can't be done with a
data edit alone: `en_menu.txt` text is looked up by an ID that is hard-coded in the
report routine, and no stock code speaks a new ID.

## Static look (2026-11-25, OAE 1.5.2 `sh3.exe`, the live install, read only)

How the stock flooding report speaks (`0x00436BCB`–`0x00436F1A`, in `0x435B30`, a method
of the report object `esi`):

1. `0x4CA800(this = 0x566058, id)` returns the text. It is a bare array lookup:
   `[[0x566058+0x130] + id*4]`, bounds-checked against `[0x566058+0x138]`. **The string
   table is static.** String 592 ("We have flooding, sir!") is the `char*` at
   `[[0x566188] + 0x940]`.
2. The text is formatted (`[0x51D70C]`, MSVCR71 `sprintf` — corrected in ticket 35; was read as `strcpy`) into the report object's buffer at
   `esi+0x35DC`.
3. The latch byte is set: `esi + 0x32B4 + idx` for band 1 (`0 < L < 0.35`), with
   `idx = movsx byte [ebp+0x566898]`, a per-compartment slot.
4. A 0x28-byte message object is built from the buffer (`0x4B79C0`) and queued on
   `[[[0x5449B0]+0xD8]+0x7EC]+0x27E8` (`0x4B7DF0`). `0x4EF9E0(0, 3, …)` follows: probably
   the speech or sound cue. Not examined.

The report routine is called from `0x43A8A7`/`0x43A8DD` with `ecx = esi`, the same object.
**Its static root is not located yet.**

## Proposed route: borrow the stock report (memory only)

When the motor room's flood first rises to 15 %:

1. Point string 592's `char*` at APE's own text in `ape.act`'s `.data`, for example
   "Water in the motor room, sir! Pump it out or stop the motors!"
2. Clear the band-1 latch for compartment 4, so the stock routine re-arms (15 % sits in
   band 1, `0 < L < 0.35`).
3. The game's own thread speaks it on its next pass: it copies APE's text, sets the latch
   and queues the message.
4. When APE sees the latch set again (or after a timeout), it puts 592's pointer back.

This is the same kind of write APE already makes (flood level, engine floats, hull). No
stock code is changed or called. Fit with ADR-0001: yes.

### Costs and risks

- **Voice.** If `0x4EF9E0` plays a speech sample chosen by the band, not by the text, the
  chief will *say* "We have flooding, sir!" while the message log shows APE's text. Not
  checked.
- **Race.** If a *real* band-1 flooding report in another compartment fires in the same
  frame window, it would show APE's text. The window is one or two report passes.
  Acceptable, but worth stating.
- **English only.** The text is in `ape.act`, not in the language's menu file.
- **Ticket 28's floor.** APE's seep floor keeps the latch from re-arming on its own;
  clearing it once on purpose doesn't conflict with that.

## RE still needed

- **The report object's static root** (`esi` at `0x435B30`): static first (callers of the
  function containing `0x43A8A7`), then a short CE confirmation if needed.
- **`0x566898`**: confirm it maps the zone/compartment index APE already uses (compartment
  4) to the latch slot.
- **What `0x4EF9E0(0, 3, …)` does**: the voice question above.
- A live check: swap, re-arm, see the text in the message log, restore.

## Decided (grilling round 1, owner, 2026-11-25)

- **Only while running.** Warn when compartment 4's flood is at or above the warning level
  **and** the ordered telegraph is not at stop (the same test as motor flooding damage,
  `[0x0063E1D4]`). Ordering ahead again with the water still above the level warns again.
- **Any water.** Like the damage (ADR-0012), it is keyed to the water in the room, stock
  breach flooding included; no need to tell APE's water from real water.
- **Level = flooding threshold − 5 points**, floor 1 %. No patcher field. Past 35 % / 60 %
  the warning sits in stock's band 2 / 3, so the route borrows the string and latch of the
  band the level is in (592 / 593 / 594).
- **Repeat:** once per crossing, re-armed when the level falls 5 points below the warning
  level (hysteresis), or when the telegraph goes to stop.
- **Wording:** "Water in the motor room, sir! Pump it out or stop the motors!", English,
  in `ape.act`. No "CE: " in the text: the game prefixes it from the speaker
  (`en_menu.txt` 609 "Chief Engineer (CE)"; 592 itself is bare). Buffer length at
  `esi+0x35DC` still to check.
- **Ticket 28's custom seepage wording stays out of scope.** If the swap is proven live,
  note in ticket 28 that it no longer needs a hook.

### Round 2

- **Off with the damage.** No warning when the motor damage amount is 0; no switch of
  its own.
- **Race accepted.** A real report in another compartment during the swap window shows
  APE's text. Keep the window short: restore on the first poll that sees the latch set.
- **Timeout.** If the latch isn't set within ~10 real seconds, restore anyway, count the
  crossing as spent, log one line to `ape_act.log`, and don't retry until the next re-arm.

Route: borrow the stock report (owner chose to move forward, 2026-11-25), gated on the
RE checkpoint in ticket 35.

## Decisions for the owner

1. **Route.** Borrow the stock report as above (recommended), or accept no in-game message
   and rely on the stock "We have flooding, sir!" (already spoken once per dive)?
2. **Warning level.** A fixed 15 %, its own patcher field (default 15), or always 5 points
   below the flooding threshold (so it follows a player's retune)?
3. **Wording.** The exact text, and whether a chief-engineer prefix ("CE: ") is expected.
4. **Repeat.** Once per crossing, re-armed when the level falls back below (say) 10 %?

## Answer

Resolved 2026-11-25. Build it by borrowing the stock flooding report: swap the band
string's pointer to APE's text, clear compartment 4's latch, and restore after it speaks.
Only while the motors are running, for any water, at threshold − 5 points, once per
crossing with 5-point hysteresis, off when motor damage is 0. Plan and RE checkpoint:
[ticket 35](35-motor-room-flooding-warning-build.md). If the voice turns out to follow the
band, the plan accepts a spoken stock line with APE's text in the log, unless the owner
vetoes it.
