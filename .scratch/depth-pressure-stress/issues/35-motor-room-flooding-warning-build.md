# 35: Build the motor-room flooding warning

Type: task
Status: ready-for-human
Blocked by: none

## Parent

Ticket 34 (grilling, resolved 2026-11-25). Stoss's "at 15 % flooding, a warning message to
pump or shut down the motors." Fits ADR-0001: memory writes only, from APE's thread; no
stock code changed or called.

## What to build

Behaviour (all decided in ticket 34):

- **Condition.** Motor damage amount > 0, **and** compartment 4's flood (the electric
  engine room, the same read as motor flooding damage) ≥ **warning level**, **and** the
  ordered telegraph `[0x0063E1D4]` ≠ 0.0. No telegraph value → no warning this poll.
- **Warning level** = flooding threshold − 0.05, floor 0.01. No patcher field. Default
  0.15 with the 0.20 threshold.
- **Once per crossing.** Re-arm when the level falls below warning level − 0.05, or when
  the telegraph goes to stop. Ordering ahead again with water still above the level warns
  again.
- **Text:** `Water in the motor room, sir! Pump it out or stop the motors!`, a static
  `char[]` in `ape.act`'s `.data`. No "CE: " in it (the game adds it from the speaker).
  English only.

Mechanism, the "borrow the stock report" route:

1. Pick the band the current level is in: `L < 0.35` → string 592, latch `+0x32B4`;
   `0.35 ≤ L < 0.60` → 593, `+0x32C0`; `L ≥ 0.60` → 594, `+0x32CC`.
2. Save that string's `char*` from the static table (`[[0x566058+0x130] + id*4]`; 592's
   is at `[[0x566188] + 0x940]`) and write APE's text pointer in its place (one aligned
   4-byte write).
3. Clear the band's latch byte for compartment 4: `report + latch + idx`, with `idx` the
   compartment-4 slot from the table at `0x566898`.
4. Restore the saved pointer on the **first poll** that sees the latch set again. If it
   isn't set within **~10 real seconds**, restore anyway, mark the crossing spent, write
   one line to `ape_act.log`, and don't retry until the next re-arm.
5. Always restore on unload/detach. Never leave APE's pointer behind.

Accepted costs: a real report in another compartment, in the same band during the swap
window, shows APE's text (race, accepted). Ticket 28's seep floor keeps band 1 latched
during seepage; clearing it once on purpose doesn't fight that.

## Built (2026-11-25), awaiting in-play acceptance

Pure decision: `src/mods/depth_stress/warning.{hpp,cpp}`, 22 tests in
`tests/flooding_warning_test.cpp`. Memory side: `game_memory.cpp` (two new optional AOB
anchors, swap and restore) and `FloodingWarning` in `dllmain.cpp`. Anchors and chains:
`docs/re-notes/11-flooding-report-swap.md`. The full suite and the Debug `ape.act` build
pass. **Not run in the game.** Needs the rebuilt `ape.act` (Release, through the
patcher) and the live checks below. Two small choices beyond the ticket: a dry room (flood
0) also re-arms, since the 1 % floor can put "level − 5 points" below zero; and a new boat
re-arms.

## Step 0 — RE checkpoint (before code)

Mostly settled statically on 2026-11-25 (see `## Comments`): root `[[0x5449B0]+0x100]`,
compartment-4 latch `+0x32B8` / `+0x32C4` / `+0x32D0`, latch-clear fires mid-band, no
voice mismatch on OAE, text must not contain `%`. The bullets below are the original
questions; only the live checks in Comments remain:

- **The report object's static root** (`esi`/`ecx` at `0x435B30`; called from
  `0x43A8A7`/`0x43A8DD`). Needed for step 3. Static trace first, a short CE confirmation if
  needed. Record an AOB per ADR-0003 for any code-derived address.
- **`0x566898`:** which index it takes (zone or compartment, 0- or 1-based) and the slot
  for compartment 4. If it maps per zone, use the zone→compartment table APE already reads.
- **Clearing the latch makes it speak** with `L` sitting mid-band (no "fresh crossing"
  condition elsewhere), and how often the routine runs.
- **The voice (`0x4EF9E0(0, 3, …)`).** If the spoken sample follows the band or id, not the
  text, the chief will *say* the stock line while the log shows APE's text. Plan: accept
  that and build anyway (the spoken line is still a flooding alarm). If the owner vetoes
  it, stop here and reopen ticket 34's route question.
- **Buffer length** at `esi+0x35DC`: APE's text (60 chars) must fit, or shorten it.

## Tests

- Unit (`tests/depth_stress_test.cpp`): the decision logic as a pure function. Covers the
  condition, warning level and its 0.01 floor, band choice at 0.35 / 0.60, hysteresis
  re-arm, telegraph-stop re-arm, amount 0 = never, the timeout giving up after one try, and
  restore on the latch being seen.
- No GUI change, so no patcher test, unless the warning level is shown read-only.

## Acceptance (in play, OAE 1.5.2)

- With the telegraph ahead, let the motor room fill past 15 %: the message log shows
  `CE: Water in the motor room, sir! …` once. Note what is said aloud.
- It doesn't repeat while the level hovers. It fires again after pumping below 10 % and
  refilling, and after stop → ahead with water above 15 %.
- With the telegraph at stop, no message.
- Afterwards, a real flooding report (any compartment) shows stock text: the pointer was
  restored. `ape_act.log` shows no timeout in normal play.

## Docs to update when done

- PRD (new step under motor flooding damage), `CONTEXT.md` (a **flooding warning** entry
  next to **motor flooding damage**), README player text, map Decisions-so-far.
- Ticket 28: note that custom seepage wording no longer needs a hook, if this route is
  proven live (owner keeps it out of scope for now).

## Comments

### Static RE results (2026-11-25, OAE 1.5.2 `sh3.exe`, read only)

Settles most of Step 0 statically; the live checks at the end remain.

- **Correction to ticket 34:** `[0x51D70C]` is MSVCR71 **`sprintf`**, not `strcpy`:
  `sprintf(esi+0x35DC, text)`. APE's text is used as a format string, so it **must not
  contain `%`**. The buffer is at most 0x130 bytes; the 60-char text fits. The message
  object takes its own copy (`0x4B7970`, strdup), so restoring the pointer afterwards is
  safe.
- **Report object root: `[[0x5449B0] + 0x100]`** (high confidence). It is the
  "Page barracks" menu page (id `0x67000000`, vtable `0x51FC60`, size 0x370C). The root's
  constructor `0x408D50` sets `[0x5449B0]` and caches `FindByID(0x67000000)` at `+0x100`
  (`0x4091D4`). Stock uses the same cache (`0x46FC3F`).
- **`0x566898` maps zone index → 0-based compartment id = latch slot.** It is in BSS and
  filled by `0x4FDDA0` (not per class). Compartment 4 = zone 30 = slot 4. Latches for
  compartment 4: band 1 **`+0x32B8`**, band 2 `+0x32C4`, band 3 `+0x32D0`.
- **Clearing the latch makes it fire** on the next pass with `L` mid-band. There is no
  "came from below" check. In bands 2/3 the routine clears the lower latches on every pass.
  **Caveat:** the latch is set *before* the crew gate `0x4E33F0(0)` (crew station 2, likely
  the chief). If that gate fails, the latch is set and nothing is shown, so "latch seen
  set" does not prove the message appeared.
- **Rate:** the page's per-frame update `0x43A2D0` calls `0x435B30` at least once per
  game-second, more often under time compression. It is skipped when time is stopped, and
  possibly when `page+0x8C != 1` (meaning unconfirmed). Outer gates are at
  `0x43A59E`–`0x43A5D2`. The ~10 s timeout covers these.
- **Voice: no mismatch on OAE.** `0x4EF9E0` queues a speech record chosen by station 0 (CH)
  and number: 5 for band 1, 4 for band 2, 3 for band 3. The text is never consulted. OAE's
  `data\Cfg\Message_CH.cfg` has `[Msg4]` and `[Msg5]` commented out, so bands 1 and 2 should
  be silent (inferred) and only band 3 speaks "critical flooding". On a stock
  `Message_CH.cfg`, the chief would say "We have flooding, sir" while APE's text shows.
  The plan already accepts that.

**Still live (CE / in play):**
- `[[0x5449B0]+0x100]` is non-null on patrol, stable across save/load, and `+0x32B8`
  toggles as compartment 4 floods.
- OAE is silent for band 1.
- The swap shows the text within about a game-second.
- What `page+0x8C` and `0x4E33F0(0)` mean in normal play.
