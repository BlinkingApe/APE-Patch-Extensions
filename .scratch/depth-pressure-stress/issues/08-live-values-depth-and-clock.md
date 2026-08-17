# Live-value session A: current depth + mission clock

Type: task
Status: resolved
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

## Question

Resolve, live, the two values every effect in the new model depends on:

1. **Current depth** — `[movementObj + 0x54]`. The static half is done: AOB
   `D9 44 24 14 D8 59 54 DF E0 F6 C4 41 0F 85 ?? ?? ?? ??` @`00513FFD` is unique
   and confirms the offset. What is missing is the **pointer chain to
   `movementObj`** — the setter `00513FC0` reaches its object via a `this`/local,
   not a static root, so this needs a CE pointer-scan.
2. **Mission clock** — address, chain, format and resolution. Must be confirmed to
   **freeze on pause** and **scale with time compression**, since the whole model
   accrues in game-seconds (a 1024× deep transit must cost what an unaccelerated one
   would).

Candidate static singleton roots already recorded: `0x5449B0`, `0x55408C`,
`0x5543DC`. `[[0x5449B0]+0xD8]` is a god/manager object, **not** the player object.

This is HITL: it needs SH3 running with a human diving the boat.

⚠ **Hash the running `sh3.exe` first** (`docs/re-notes/01-live-session-playbook.md`).
Umbrella ticket 11 lost a session to an unverified binary; ticket 12's hash-first
run had every address resolve first try.

## Static leads (2026-08-17)

`0x5543DC` (candidate root) narrowed further: it's read at both `00513FCC` (inside
the depth setter, as a tolerance float off `+0x10`) and `00514560` (the setter's
caller, as a liveness guard) — the exact function neighbourhood `movementObj`
lives in, though not proven to *be* `movementObj`. No static candidate found for
the mission clock. Full writeup: `docs/re-notes/01-crush-depth-damage-signature.md`
§6.

## Static leads (2026-08-17, second pass): negative result — no shortcut exists

Checked whether ticket `09`'s newly-proven `0x554A88` boat chain also reaches
`movementObj` (sparing a second pointer-scan here). It does not: exhaustive forward
and backward xref checks around `0x554A88` and its chain's final hop found zero
references to a `[reg+0x54]` operand anywhere. Separately, the depth-setter's
caller (`00514560`) was traced and found to have **zero direct callers anywhere in
the binary** — it's reached only through an untraceable vtable slot
(`.rdata:0x5322E0`), which closes the static path definitively rather than
narrowing it. **A from-scratch CE pointer-scan is required for `movementObj`; no
static shortcut exists.** One minor triangulation point for that session:
`[this+0x60]` is a small refcount/tick guard touched in both the setter and its
caller — worth watching alongside `ecx`/`esi` while dumping live, but not a root.

Mission clock: still no static candidate after two passes. The one candidate this
note had flagged (`+0x358`/`+0x360` doubles off `[[0x5543DC]+4]`, §6.3) turned out
to be a bounded max/current accumulator (reads like an equipment wear ratio), not a
monotonic clock — ruled out and flagged instead as a possible lead for ticket `11`.
The mission clock needs a live Exact/Increased-Value scan from scratch, same as
originally planned; static analysis has nothing further to offer it. Full writeup:
`docs/re-notes/01-crush-depth-damage-signature.md` §8.

## Static lead (2026-08-17, not yet chased): NSAP's `sh5.act` may already solve this

The live pass's exact-value depth scan surfaced NSAP's own `handleShallowWater`
routine reading a cached `sh5.uBoatDepth` (§10.2 of the RE note) — a red herring
for a *scan*, but potentially not for a *static read*: NSAP has to resolve the
real `movementObj`/boat every frame too, the same way h.sie's hull-repair cave
already had the boat chain that unblocked ticket `09` (§7). Worth a static pass on
`sh5.act` before the next live session: trace backward from `sh5.uBoatDepth`'s
writer to see what it reads from, and whether that source is rooted in something
static (either in `sh5.act` or `sh3.exe`) rather than another this-relative lookup.
If it pans out, it may shortcut the pointer-scan the live session still owes;
if NSAP resolves it just as dynamically as we'd have to, this is a dead end and
the live pointer-scan (§10.2) remains the only path.

## Acceptance criteria

- [x] `movementObj` resolved via a chain rooted in stable stock `sh3.exe`;
      `[movementObj + 0x54]` confirmed against the depth gauge at several depths.
- [x] Mission clock located; chain, format and resolution recorded.
- [x] Mission clock confirmed frozen under pause and scaling under time compression.
- [x] Both recorded in `docs/re-notes/01-crush-depth-damage-signature.md` against the
      **SHA-256 of the binary actually running**.

## Resolution (2026-08-17)

Both live values are resolved, static-root chains proven across two relaunches.
Full write-up: `docs/re-notes/01-crush-depth-damage-signature.md` §11 (depth) and
§10.1 (mission clock, resolved in the prior session).

- **Current depth:** `[[["sh3.exe"+002307BC]+4]+4]+0x54` — 3-level static chain,
  found via CE pointer-scan + two-relaunch pointermap comparison (84 candidates
  narrowed to 3, the shortest ending in the already-known `0x54` field offset).
  Float, meters. Confirmed live-tracking the depth gauge; confirmed stable across
  two independent relaunches.
- **Mission clock:** `0x0063FF80` (`sh3.exe+23FF80`), static VA, no chain needed.
  4-byte integer, 1:1 with game-seconds at 1× time compression. Confirmed frozen
  on pause, confirmed scaling at 512× time compression, confirmed stable across
  relaunch.
- Binary hash re-verified at session start: SHA-256
  `eeef8595a75d2c95d768867f7c3eddea48b7940bd3d82ca2d159857476c68b61`, identical to
  the prior session's build.

## Notes

Old issue `01` carries the static-pass write-up and the four original hunts; the CDT
hunt in it is **dead** — the new model never reads crush depth.
