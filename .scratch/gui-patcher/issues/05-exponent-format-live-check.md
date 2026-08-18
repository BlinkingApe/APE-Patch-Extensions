# Exponent format: live check against a real install

Type: task
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

Does h.sie's `hsie.ini` parser accept APE's two-digit scientific-notation
exponent (`4.1667e-08`), or does it require h.sie's own shipped one-digit
form (`4.1667e-8`)? Ticket 01 raised this and left it explicitly unproven —
the beta tester's rate reached `hsie.ini` by hand, not through APE, so
APE's own write format was never exercised against the real parser.

This is a HITL task, not a design decision: it needs one patch through the
GUI at a diesel-damage rate high enough to produce visible in-game damage
within a normal test window (per the blackbox procedure in
`.scratch/umbrella-roadmap/issues/12-diesel-levers-blackbox-test.md`),
against a real OAE 1.5.2 install, with the result observed in play.

## Answer should record

- Whether the two-digit exponent worked as written, or needed a fix.
- If a fix is needed, whether it belongs in this v1.1 pass or is a
  same-day 1.0.x-style fix (it directly affects whether diesel damage works
  at all, unlike the polish items — flag this distinction when resolving).

## Answer

**PASS.** Run 2026-08-18 by the user against a real OAE 1.5.2 install. APE
wrote `Val9=1.0000e-02` (two-digit exponent, as `Fmt.Sci()` produces) through
its own GUI write path. Damage occurred after ~3 in-game minutes — correct,
consistent with ticket 12's blackbox rig at a comparable rate. h.sie's
parser accepts APE's two-digit scientific-notation format as written;
no fix needed.

While observing this run, the user raised a follow-on UX item (the
pre-damage warning message's wording/timing) — see [ticket 09](09-diesel-damage-report-message.md),
added to the map as a new grilling ticket rather than folded into this
task's answer.
