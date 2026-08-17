# Live-value session A: current depth + mission clock

Type: task
Status: open
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

## Acceptance criteria

- [ ] `movementObj` resolved via a chain rooted in stable stock `sh3.exe`;
      `[movementObj + 0x54]` confirmed against the depth gauge at several depths.
- [ ] Mission clock located; chain, format and resolution recorded.
- [ ] Mission clock confirmed frozen under pause and scaling under time compression.
- [ ] Both recorded in `docs/re-notes/01-crush-depth-damage-signature.md` against the
      **SHA-256 of the binary actually running**.

## Notes

Old issue `01` carries the static-pass write-up and the four original hunts; the CDT
hunt in it is **dead** — the new model never reads crush depth.
