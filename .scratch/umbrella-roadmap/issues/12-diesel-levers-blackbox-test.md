# Black-box test: prove APE controls diesel-damage magnitude and timing

Type: task
Status: resolved

## Blocked by

None. Supersedes ticket 11 as the actual pre-ship gate for diesel damage —
see ticket 07, which should point its blocker here instead.

## Question

Raised 2026-08-16 as the resolution of
[ticket 11](11-surfaced-gate-proof.md), which spent two sessions trying to
establish the identity of the diesel cave's surfaced-gate check
(`[0x5543DC+0x18]`/`+0x1C` vs `0.0` at `0x6350CA`) and hit diminishing
returns — static exhausted, live broke on an unverified binary hash and a
breakpoint that never fired. The user's call: stop proving the mechanics from
first principles, trust h.sie's developer got the gate and damage model
right, and instead prove the one thing APE actually needs guaranteed — that
its own patched values (damage magnitude, timing/rate) take effect and
produce the expected in-game outcome. This is testable without resolving the
gate's identity.

## Prep in progress before the session (2026-08-16)

The user is handling two prerequisites between now and whenever ticket 12 is
picked up, ahead of any agent involvement:

- **Backing up `sh3.exe` and `hsie.ini`** from the OneAlex Extended 1.5.2 game
  root before either file is touched, so step 2/3's writes (live CE poke or
  direct edit) can be made freely and reverted afterward without risk to the
  install.
- **Confirming the h.sie Diesel Damage fix is already applied** to that same
  `sh3.exe` — the one in the game root, not a separate copy — before the test
  writes land. Steps 2/3 assume the diesel cave and its rewritten probability
  function (`0x635818`) are live in the binary being tested; if the fix
  isn't applied first, the forced magnitude/rate values have nothing to hook
  into and the test would silently prove nothing.

Whoever claims this ticket should confirm both are done (and that the backed-up
copy matches the hash check in step 1 below) before writing anything.

**This is a HITL task** — the agent cannot run the game. Checklist for
whoever runs the session:

1. **Hash the running `sh3.exe` first.** Compare against the build
   `docs/re-notes/04-diesel-damage-signature.md` and ticket 11's addresses
   were derived from (`01-live-session-playbook.md` names the expected
   build). If it doesn't match, re-establish the sites below against the
   actual running binary before poking anything — this exact skip is what
   burned ticket 11's 2nd session.
2. **Force a deterministic magnitude.** Set the damage-band range to
   **lo = 1.0, hi = 1.0** — the four `.pcode` immediates at
   `0x6351B1`/`B6`/`C3`/`CA` (see `04-diesel-damage-signature.md`), or the
   equivalent live CE poke if the GUI patcher isn't built yet. This removes
   the RNG: any hit is full destruction, unambiguous to observe.
3. **Force a short interval.** Set the rate parameter (the `hsie.ini` `ValN`
   key ticket 09 mapped for this field) high enough that the rewritten
   probability function (`0x635818`, ticket 04's clamped `p × TC`) makes a
   hit near-certain within roughly the first 10 in-game minutes.
4. **Get underway, surfaced, at any nonzero telegraph.** Speed no longer
   matters — ticket 04 flattened the MTS exponent to `0` — so any throttle
   above stop satisfies "diesels running." Surfaced is the mod's advertised
   operating condition; that's the condition being tested here, not the gate.
5. **Advance ~10 in-game minutes and observe.** Expect the same sequence
   ticket 11's 2nd session already confirmed fires (warning message → "Xx
   Diesel Engine Damaged" → crew screen shows the engine damaged/destroyed).
   A hit within the window confirms APE controls both levers. No hit is
   itself a major finding — escalate back to ticket 11's territory rather
   than assuming a fluke.
6. **While still surfaced, try to repair.** Assign crew to the damaged
   engine and watch whether the damage float moves off `1.0`. This directly
   answers ticket 11's still-open question — whether exactly `1.0` is
   repairable at sea or needs base repair — since step 2 guarantees the hit
   lands at exactly `1.0`.
7. **Optional, not required for closure:** if convenient in the same
   session, repeat once submerged (non-snorkel). This is free bonus data on
   the gate question ticket 11 set aside on trust — not a requirement, and a
   null result here doesn't block closing this ticket.

## Test setup applied (2026-08-16, agent session)

Prerequisites verified before writing:

| Check | Result |
|---|---|
| Backups present | `sh3_BACKUP.exe`, `hsie_BACKUP.ini` in the game root |
| Backup `sh3.exe` hash | `51d14ca3c08f42c9…` — **exact match** for the binary `04-diesel-damage-signature.md` was derived from |
| Live `sh3.exe` vs backup | differs by **exactly one byte**: `0x14EF13` `00`→`FF` |
| That byte | flag table entry #20 = **Diesel Damage Fix — ON** (table base `0x14EF00` per `HSIEContents.txt`) |
| h.sie version stamp (`0x14EC00`) | `v 1.6E1` |
| `0x635818` helper | still h.sie's stock 21-byte `1-(1-p)^TC`; APE's rewrite deliberately *not* applied (unnecessary at this test's `p`, five orders of magnitude above the 2⁻²⁴ lattice) |

**Step 2 — magnitude forced to exactly 1.0.** All four `.pcode` immediates
written to `00 00 80 3f`, after verifying the surrounding `push` / `mov`
opcodes matched stock:

| Site | File offset | Was | Now |
|---|---|---|---|
| first-hit hi | `0x1471B1` | `3d0a173f` (0.59) | `0000803f` (1.0) |
| first-hit lo | `0x1471B6` | `0ad7233c` (0.01) | `0000803f` (1.0) |
| escalated hi | `0x1471C3` | `0000803f` (1.00) | unchanged |
| escalated lo | `0x1471CA` | `9a99993e` (0.30) | `0000803f` (1.0) |

`randRange(1.0, 1.0)` → `1.0`; `fadd [edx]` onto a pristine `0.0` → `1.0`;
clamp is a no-op. Any hit destroys outright, from any prior state.

**Step 3 — rate flattened to APE's model and raised.** Written to `hsie.ini`
(the effective store for these — ticket 09), keys surgically edited, comments
and file shape preserved:

| Key | Was | Now | Why |
|---|---|---|---|
| `Val9` (`p_dso`) | `2e-4` | `0.01` | rate lever |
| `Val11/12/13` (Factor) | 1.0 / 4.0 / 8.0 | 1.0 / 1.0 / 1.0 | remove escalation |
| `Val14/15/16` (Speed-Exponent) | 200 / 20 / 10 | 0.0 / 0.0 / 0.0 | ticket 04's flattening — makes risk telegraph-independent |
| `Val10` (threshold) | `1e-6` | unchanged | `p_ds` = 0.01 clears it by 4 orders |

`p_ds = 0.01 × 1.0 × Speed⁰ = 0.01` per game-second at **any** telegraph above
stop. T50 = **69 game-seconds**; P(hit) = 45% at 1 min, 95% at 5 min,
**99.76% at 10 min**.

### ⚠ Expected observables differ from step 5's original wording

Forcing the magnitude to exactly `1.0` **suppresses h.sie's warning string** —
`0x6351EE` does `cmp [edx], 1.0; je`, so string 4893 ("If we don't cut power
now…") fires on *partial* damage only. Do not read its absence as a failure.
What should be seen instead, in order:

1. **Time compression drops to 1×** — `0x6351E7` force-writes `1.0` to the TC
   manager on every hit. This is the sharpest signal, and it is unconditional.
2. The game's own engine-damage report / "Diesel Engine Damaged".
3. Crew or damage screen showing that diesel **destroyed** (not partial).
4. Because both bands are now `1.0` and factors are flat, expect the **second**
   diesel to be destroyed within another minute or two; after both reach `1.0`
   the cave exits at `ebp >= 2` and nothing further happens. Two hits is two
   confirmations of the lever, not a bug.

Resolved when steps 2–6 have been run and the outcome (hit or no hit, and the
repair result) is recorded — either result closes this ticket; a no-hit
result should also reopen or link back to ticket 11's gate-identity question
rather than being treated as a simple bug.

## Answer

**PASS. APE controls both diesel-damage levers.** Run 2026-08-16 by the user
against OAE 1.5.2 (`v 1.6E1`), surfaced and under way, with the setup recorded
above. Full destruction landed at **~6 in-game minutes**, inside the predicted
10-minute window.

### What this proves

- **Magnitude — proven exactly.** The four `.pcode` immediates at
  `0x6351B1/B6/C3/CA` are the damage band, and a static write to them takes
  effect in play. Forced `rand(1.0, 1.0)` produced a diesel destroyed outright
  on its first hit — the unambiguous outcome, from a pristine engine, which
  h.sie's stock `0.01…0.59` first-hit band **cannot** produce at all. The
  observed result is only reachable via the written bytes.
- **Timing — proven.** `hsie.ini`'s `Val9`/`Val11–16` are the live rate
  controls. Stock tuning gives T50 = 58 min at telegraph 1.00 and is
  *mathematically inert* below 0.97; the run produced a hit in ~6 min at an
  ordinary telegraph setting. Both the rate raise (`Val9` 2e-4 → 0.01) and
  ticket 04's exponent flattening (`Val14–16` → 0.0, which is what makes any
  nonzero telegraph dangerous) had to be in effect for that outcome.
- **Ticket 09's routing decision is confirmed in play.** Two stores, one
  feature, written in their two different ways — `hsie.ini` keys for the rate
  (inside the `.pconst` clobber list) and `sh3.exe` `.pcode` for the band
  (outside it) — and both stuck. This is the first end-to-end evidence that
  the per-site store rule works on a real install, not just on paper.
- **The prerequisite hash discipline paid off.** Backup `sh3.exe` matched
  `51d14ca3…` exactly, and the live binary differed by precisely the one flag
  byte. Every address in `04-diesel-damage-signature.md` resolved correctly on
  the first attempt — the opposite of ticket 11's 2nd session, where this check
  was skipped.

### What this does not prove

- **Rate accuracy — one sample, no curve.** ~6 min against a T50 of 69 s sits
  on the slow side of the predicted distribution (roughly the 97th percentile).
  Entirely consistent with a single draw, and partly expected — the cave
  **discards any tick where time compression changed** (`0x6350BE`), which is
  frequent while getting under way. But nothing here validates the rate
  *quantitatively*, and nothing needs to: APE's shipping rate is a user-facing
  tuning field, not a claim requiring proof.
- **Step 6 (repair at exactly `1.0`) was not run** — the session ended before
  the repair attempt. So ticket 11's side question, whether a diesel at exactly
  `1.0` is repairable at sea or needs base repair, is **still open**. Carried to
  the map's fog. It does not gate this ticket: APE's shipping band tops out at
  `0.55`, so it never produces a `1.0` from a single hit, and the question only
  bites the two-unrepaired-hits tail (3.1%).
- **Step 7 (submerged / snorkel) was not run** — explicitly optional, and a
  null result there was never a blocker. The surfaced-gate identity stays
  exactly where ticket 11 left it: a named, accepted risk. The user notes the
  same rig can be reused for a snorkel run whenever convenient.

### Consequence

**Ticket 07's blocker on diesel damage is lifted.** The feature may enter the
v1 patcher list on its merits; it remains the candidate exercising the most of
the architecture (two write targets, five `sh3.exe` sites including the 21-byte
rewrite, eight `hsie.ini` keys, two validated GUI fields).

### ⚠ The install is still in test state

`sh3.exe` and `hsie.ini` in `C:\games\OneAlex Extended 1.5.2` still carry the
forced values above — left in place deliberately so the snorkel run can reuse
the rig. **Do not play normally until reverted**: every diesel hit destroys an
engine outright, at any speed. Revert by copying `sh3_BACKUP.exe` → `sh3.exe`
and `hsie_BACKUP.ini` → `hsie.ini` (both verified identical to pristine at
setup time, apart from the Diesel Damage flag byte the user had already set).

## Follow-up testing, same session (2026-08-16) — four more conditions

The user continued past step 5 with the rig still in place. Results:

| # | Condition | Outcome | Correct? |
|---|---|---|---|
| 1 | Surfaced, **all stop** (diesels not running) | destroyed < 10 min | ❌ **wrong** |
| 2 | Submerged, stationary | no damage | ✅ |
| 3 | Submerged, electric motors | no damage | ✅ |
| 4 | Submerged, **diesels** (snorkel), any speed | destroyed < 10 min | ✅ |

### Finding A — `0^0 = 1`: ticket 04's flattened exponent has no idle gate

**Case 1 is a genuine defect in the model, not in the test rig.** With
`Speed-Exponent = 0.0`, the formula `p_ds = p_dso × Factor × Speed^0` evaluates
`pow(0.0, 0.0)`, and CRT `pow` returns **`1.0`** — the IEEE-754 / C99 mandated
result. So at all stop the mechanic runs at **exactly the full flank rate**.

Ticket 04's whole premise is "wear whenever the diesels run", and it used the
flat exponent to mean "at any speed". It silently also means **at no speed**.
This was invisible before this test because h.sie's own tuning (`Speed^200`)
sends `p_ds` to zero long before the telegraph reaches 0, and ticket 04's
reasoning never examined the `Speed = 0` endpoint.

⚠ Note the shape of the mistake: this is the *second* time an endpoint of
ticket 04's retuning has bitten (the first was the `2.98e-8` float32 lattice
floor in `0x635818`). Both were invisible at h.sie's tuning and appeared only at
APE's. **Retuning a h.sie parameter to an extreme of its range needs its
endpoints evaluated, not just its midrange.**

### Finding A's fix — cutoff at telegraph 0.1, no code patch required

The user asked for a gate at telegraph **> 0.1**: above OAE's minimum
(`AheadSlow = 0.20`) and KC's (`0.40`) by a wide margin, while excluding any
value that is "technically not equal to 0".

**This needs no new code.** h.sie's existing threshold test at `0x635129`
(`damage iff p_ds > Val10`) can be made a speed cutoff by pairing a *small
positive* exponent with a recomputed threshold:

```
Speed-Exponent (Val14/15/16) = 0.01
Threshold      (Val10)       = p_dso x 0.1^0.01 = p_dso x 0.977237
```

Because `Speed^0.01` is monotonic, `p_ds > Val10` ⟺ `Speed > 0.1` exactly. And
because the exponent is tiny, the model stays flat to within **1.6% across the
entire usable speed range**:

| Telegraph | | `p_ds` vs flank | damage? |
|---|---|---|---|
| 0.00 | all stop | 0% (`pow(0, 0.01) = 0`) | **no** |
| 0.10 | cutoff | 97.72% | no (boundary excluded) |
| 0.20 | OAE Ahead Slow | 98.40% | yes |
| 0.40 | KC minimum | 99.09% | yes |
| 1.00 | Ahead Flank | 100.00% | yes |

Applied to the test rig: `Val14/15/16 = 0.01`, `Val10 = 0.0097724`
(for the rig's `p_dso = 0.01`). Retest case 1 to confirm.

Precision is not a concern at either tuning. The margin between the threshold
and `p_ds` at telegraph 0.20 is 0.7% relative, ~58,000× float32's relative
precision, and the comparison itself runs at 80-bit on the x87 stack — only the
two stored operands are float32.

⚠ **Consequence for the patcher (tickets 07/08).** `Val10` is no longer a
constant: it is **derived from `p_dso`**, so APE must recompute and rewrite it
whenever the user changes the rate field. It becomes a *computed* manifest site,
not a user-facing one — the first site of that kind. The alternative — patching
an explicit `fcomi` against 0.1 into the cave — decouples them but costs bytes
and a second code-rewrite site; **not chosen**, but recorded here as the fallback
if the derived-value coupling proves awkward in the GUI. This also assumes all
three Factors stay equal (ticket 04 sets them to 1.0); if they ever diverge, one
threshold can no longer express one cutoff speed.

### Finding B — the surfaced gate is real, works, and counts snorkelling

**Cases 1–4 together are strong black-box evidence on ticket 11's question**,
obtained without resolving the identity of `[0x5543DC+0x18]`/`+0x1C` at all.

The decisive pair is **case 1 vs case 2**: identical telegraph (all stop),
opposite depth, opposite outcome. Under the exponent-0 rig the probability term
was *identical* in both — full rate — so the only thing that can have suppressed
case 2 is the gate at `0x6350CA`. Therefore:

- **The gate is not dead and not always-pass.** This was the live risk ticket 11
  accepted on trust, and the reading the 2nd live session's frozen, identical
  field values had suggested. That session ran against an unverified binary
  hash; this one did not.
- **The gate is depth/propulsion-sensitive and behaves correctly** — cases 2 and
  3 blocked, case 4 passed.
- **Snorkelling counts as diesel running time** (case 4). Ticket 11 raised this
  as an explicit sub-question and it is now answered, affirmatively, by
  observation.

⚠ **Scope of the claim.** This establishes the gate's *behaviour*, not its
*identity* — `[+0x18]`/`[+0x1C]` remain unidentified fields, and this is four
observations, not a proof. It also rests on the reading that case 4's "submerged
using diesels" means snorkelling at periscope depth (the only way SH3 runs
diesels below the surface) — worth confirming, since if diesels can be run
submerged *without* a snorkel and still cause damage, case 4 flips from
confirming to contradicting.

But the risk ticket 11 accepted was specifically **"diesels take damage while
submerged on electrics"**, and case 3 tests exactly that condition and shows no
damage. **That risk is retired by observation.** Ticket 11 stays closed; the
map's fog entry is updated rather than reopened.

## Final results (2026-08-16) — all steps confirmed, ticket fully closed

Retest after the idle-cutoff fix (`Val14/15/16 = 0.01`, `Val10 = 0.0097724`):

| Condition | Outcome | Correct? |
|---|---|---|
| Engines not running (all stop) | **no damage** | ✅ fixed |
| Any speed above the 0.1 cutoff | full damage < 10 min | ✅ |
| Repair attempt on a destroyed (`1.0`) diesel | **no repair possible** | — see below |

**Finding A's fix is confirmed in play.** The `Speed^0.01` + derived-threshold
cutoff does exactly what it was designed to do — it suppresses the idle case
without measurably changing the rate at any real telegraph setting. h.sie's
existing threshold test carries an idle gate with **no code patch and no new
manifest site**, only a recomputed value in one already-written key.

### Step 6 answered: `1.0` is not repairable at sea

This was ticket 11's still-open side question, and the forced band guaranteed a
hit landing at exactly `1.0` to test it. **Crew assignment does not move the
damage float off `1.0`.** Destruction is terminal for the patrol.

⚠ Untested, and the distinction matters: whether **base repair** clears it. SH3's
normal convention is that a destroyed compartment/equipment is repairable only in
port, so the likely reading is "dead for the rest of the patrol, restored on
return" rather than "dead forever". Nothing here distinguishes the two.

**This validates ticket 04's core design constraint — and sharpens it.** The
historian's "damage must stay repairable" is now a *proven* requirement rather
than a stylistic preference: reaching `1.0` costs the player an engine for the
remainder of the patrol, with no counterplay. It also makes h.sie's own shipped
escalated band (`0.30…1.00`, which destroys outright 29–43% of the time from
realistic priors) look much harsher in hindsight than the numbers alone suggest.

⚠ **Raised for tickets 04/07, not decided here:** APE's band is `0.15…0.55`, so
one hit never destroys — but **two unrepaired hits reach `1.0` 3.1% of the time**,
and that tail is now known to cost an engine outright rather than merely hurt.
Whether that is acceptable, or whether `hi` should be capped below `0.5` so two
hits *cannot* sum to `1.0`, is a tuning judgement for the feature's own scope
work. Note capping at `0.5` only defers it — three hits still reach `1.0`.

### Ticket status

All of steps 2–6 are now run and recorded. Step 7 (submerged/snorkel) was run as
part of the follow-up block above and passed. **Nothing outstanding.**
