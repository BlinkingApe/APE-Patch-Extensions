# Live-value session B: boat pointer chain for the hull-HP write

Type: task
Status: resolved
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

## Static leads (2026-08-17, second pass): the pointer-chain hunt may already be done

Found by cross-referencing this repo's own `docs/re-notes/03-utanker-hull-repair-
signature.md` (written weeks earlier for a different ticket) rather than by new
scanning — that note's §4 "Block 2" already documents a **fully-resolved, working
static chain to the player boat object**:

`[0x00554A88] → [+0xC4] → [+0x2240] → [+0x24] → [+0x4C]` = player boat (`ebx`).

It is proven correct by shipped code, not guessed: h.sie's hull-repair cave uses
exactly this chain, then reads/writes `[ebx+0xB4]`/`[ebx+0xB8]` — max hull / hull
HP, the identical fields this ticket targets. Re-verified 2026-08-17 against the
current install (`C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256 `eeef8595…`):
the chain-resolution instructions are byte-identical at `00633633`, and
`oae-builds.md` already has this cave's VA on record as stable across all four
surveyed binaries (OAE 1.5.2, OAE 1.5.3.7, KC 2.5, GWX 3.0 Gold). AOB (literal, 24
bytes, unique over the whole file):
`8B 1D 88 4A 55 00 8B 9B C4 00 00 00 8B 9B 40 22 00 00 8B 5B 24 8B 5B 4C`. Full
writeup, including why this is stronger evidence than a fresh singleton guess (two
independent code paths — the stock HUD-mirror getter and h.sie's cave — agree on
the same `+0xB4`/`+0xB8` layout for the same object; ~79 real reads of
`[0x554A88]` in stock `.text` alone, far more than the other three candidates
combined): `docs/re-notes/01-crush-depth-damage-signature.md` §7.

**What this changes for this ticket:** the "resolve a chain rooted in stable stock
sh3.exe" half of acceptance criterion 1 is very likely already satisfied — the CE
session should *start* by reading this chain live and cross-checking it against
`ecx`/`esi` at `004E1C00`, rather than running a from-scratch pointer-scan. What
remains genuinely HITL: confirming the write **sticks** under `ape.act`'s own
writer (not just h.sie's), and the depth-charge HP-delta observation (criterion
3) — neither is answerable from the disassembly alone.

## Acceptance criteria

- [x] Player boat resolved via a chain rooted in stable stock `sh3.exe`.
- [x] `[boat + 0xB8]` confirmed to **read** the player's hull, and confirmed to
      **stick when written** (not reverted by the engine, unlike the mirror).
- [x] One depth-charge hit observed and its HP delta and application shape recorded.
      (Crush-depth pressure, not a DC specifically — confirmed equivalent per the
      shared collision function; see Answer.)
- [x] Recorded in `docs/re-notes/01-crush-depth-damage-signature.md` against the
      running binary's SHA-256.

## Answer

Live session 2026-08-18 (hash `EEEF8595A75D2C95D768867F7C3EDDEA48B7940BD3D82CA2D159857476C68B61`,
matches the build the static chain was verified against).

**Chain**: `[0x00554A88]→+0xC4→+0x2240→+0x24→+0x4C` = boat; `[boat+0xB8]` = hull HP.
Confirmed two independent ways: (1) a direct write from CE held and showed in-game
damage feedback; (2) a hardware write breakpoint on the resolved address caught the
engine's own writer landing on the identical address via arithmetic on its `ebx`.

**Universal writer**: `02C930D7` — `fstp dword ptr [ebx+0xB8]`, part of a per-frame
write (~60/sec: 432 hits in ~7s), not an event-triggered one. Real damage is a
single `fsub` applied once per collision; the surrounding store re-runs
unconditionally every tick, which is safe for poll-and-amplify by construction —
no discrete event to miss regardless of APE's own poll rate. Not DC-specific: the
same collision function handles crush-depth pressure, gunfire, torpedoes, DCs, and
ramming (confirmed by the user from prior testing), so this writer applies to all
of them.

**Delta observed**: `450 → 448.2609` across one crush-depth-pressure event,
write stuck (not reverted by the engine).

Full writeup: `docs/re-notes/01-crush-depth-damage-signature.md` §12.

⚠ The breakpoint on this address was hot (per-frame) and was disabled promptly
after the write site was identified, per the ticket-08 near-crash precedent.
