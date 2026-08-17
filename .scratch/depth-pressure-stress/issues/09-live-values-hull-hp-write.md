# Live-value session B: boat pointer chain for the hull-HP write

Type: task
Status: open
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

## Question

Resolve the pointer chain to the **player boat object**, so APE can both read and
**write** `[boat + 0xB8]` (hull HP) — the value the damage multiplier operates on.

Static half done: AOB `8B 81 B8 00 00 00 A3 28 6A 56 00` @`004E1C99` is unique and
confirms the offset. The getter `004E1C00` (one caller, `004F9559`) reaches the boat
via a `this`/local, so the chain needs a CE pointer-scan.

**Read vs write matters here.** `[0x00566A28]` is a static, always-current
**HP-value mirror**, but writes to it are overwritten by the engine — it serves reads
and plausibility gates only. The multiplier needs a real write, so the chain is
required regardless. Worth spending ten minutes confirming whether the mirror is
adequate for the *read* half, which would simplify the loop.

Also confirm the practical shape of poll-and-amplify:

- How large is a depth-charge HP drop in one tick, at APE's expected poll rate?
- Does the engine apply it as one step or bleed it over several frames? (A bled
  application means APE amplifies each fragment, which is fine, but the arithmetic
  should be observed rather than assumed.)
- Is a plain read-modify-write on the live float observably safe from the loop
  thread? The old PRD accepted the race on the grounds that an aligned float store is
  atomic on x86 and the per-iteration delta is small.

HITL: needs a live boat taking real damage.

⚠ Hash the running `sh3.exe` first.

## Static leads (2026-08-17)

A static pass (not yet CE-confirmed) found `0x5543DC` — already an unidentified
singleton in this note's §2 — read directly inside `00513FC0` (the depth setter)
and its caller `00514560`, and has exactly one writer in the whole binary
(`004A2966`, shaped like a unit-registration site). Not proven to equal the boat
pointer, but worth dumping and comparing to `ecx`/`esi` at `004E1C00`/`00513FC0`
**before** a blind pointer-scan. Full writeup:
`docs/re-notes/01-crush-depth-damage-signature.md` §6.

## Acceptance criteria

- [ ] Player boat resolved via a chain rooted in stable stock `sh3.exe`.
- [ ] `[boat + 0xB8]` confirmed to **read** the player's hull, and confirmed to
      **stick when written** (not reverted by the engine, unlike the mirror).
- [ ] One depth-charge hit observed and its HP delta and application shape recorded.
- [ ] Recorded in `docs/re-notes/01-crush-depth-damage-signature.md` against the
      running binary's SHA-256.
