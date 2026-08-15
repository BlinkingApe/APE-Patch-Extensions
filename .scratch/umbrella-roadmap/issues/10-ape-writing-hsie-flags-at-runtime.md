# May APE write h.sie's flag table in live memory?

Type: grilling
Status: resolved

## Blocked by

None.

## Answer — moot, 2026-08-15

**Closed without adjudication: the conflict evaporated.**

This ticket existed for exactly one reason — ticket 04 had resolved the diesel
goal onto a mechanism whose whole point was APE holding `0x63CF13` at `0x00` and
writing `0xFF` when an eight-hour overload accumulator opened a gate. That put it
in direct contradiction with ticket 05's "APE reads h.sie's flag table but never
writes it".

Ticket 04's **2nd-pass resolution removes the gate entirely.** The mod is now a
memoryless per-game-second wear rate delivered as eight edited `hsie.ini` keys —
no accumulator, no runtime writes, no `ape.act` code, and no reason for APE to
touch `0x63CF13` at any time.

So:

- **Ticket 05's rule stands unmodified.** APE reads the flag table and never
  writes it. `HsieOptionsSelector` keeps that job, and a patch whose h.sie
  dependency is off is shown disabled with an explanation.
- **"APE replacing or subsuming hsieOptions" stays out of scope**, and is no
  longer under tension.
- The four questions below are **not answered** — they are withdrawn. Nothing on
  the current route requires APE to write a h.sie flag, in memory or on disk.

⚠ If a future feature does need runtime flag writing, reopen this ticket rather
than assuming the boundary was settled. It was never argued; it was vacated.

## Question (withdrawn)

Ticket 05 ruled, deliberately and with reasons, that APE is **APE-only in scope**:
it *reads* h.sie's flag table at `0x63CF00` but **never writes it**;
`HsieOptionsSelector` keeps that job, and a patch whose h.sie dependency is off
is shown **disabled with an explanation** rather than silently enabled.

Ticket 04 then resolved the diesel-damage goal onto a mechanism whose entire
point is APE writing `0x63CF13` — h.sie's Diesel Damage flag — holding it at
`0x00` until an overload accumulator passes eight hours, then writing `0xFF`.

These cannot both stand as written. Decide where the line actually is:

- **Is "write in live memory" genuinely a different act from "write on disk"?**
  The case for yes: nothing about the user's install changes, `HsieOptionsSelector`
  still owns the persistent setting, and the write is APE's own runtime behaviour
  — the same category as any other value `ape.act` pokes. The case for no: the
  observable effect in game is identical, and a user who set Diesel Damage off in
  `HsieOptionsSelector` would find it running anyway.
- **If yes, what constrains it?** Candidate rule: APE may write a h.sie flag only
  while the game is running, only for a patch the user has enabled in APE's own
  UI, and must restore whatever it found on exit. Is that the rule, or is it
  narrower — e.g. only flags APE has fully reverse-engineered?
- **What is the user-facing story when the two disagree?** If Diesel Damage is
  `0x00` on disk and APE wants to gate it, does APE (a) require the user to
  enable it in `HsieOptionsSelector` first and keep 05's "disabled with an
  explanation" behaviour, or (b) take ownership of the flag entirely and tell the
  user to leave it off? Note the user's stated intent is to **ship with it on**,
  which makes (a) viable — but (a) means the mod runs ungated for anyone who
  enables it without APE loaded.
- **Does it generalise past diesel?** If APE may gate one h.sie patch at runtime,
  the same trick works on all 22. That is a large capability to acquire as a side
  effect of one feature — and ticket 05 explicitly ruled "APE replacing or
  subsuming hsieOptions" **out of scope**. Does this reopen that, or is
  runtime-gating narrow enough to stay clear of it?

Whichever way this lands, **update ticket 05's recorded decision and the map's
Decisions-so-far** rather than leaving two entries that contradict.
