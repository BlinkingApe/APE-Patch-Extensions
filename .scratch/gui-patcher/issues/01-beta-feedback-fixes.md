# Beta feedback fixes: commit atomicity, foreign-write detection, and the rate ceiling

Type: task
Status: ready-for-agent

## Blocked by

None

## Problem Statement

The GUI patcher's first beta (commit `9165c5b`) was tested against a real OAE 1.5.2
install on 2026-08-17. Both patches worked: diesel damage fired within 10 in-game
minutes at ahead slow with damage in the expected range, U-tanker repair with a
0–5% range restored a 400/500 hull to 415 (3% of max, in band), and unchecking
both restored stock behaviour. The tool is sound.

The session surfaced five problems, one of them found only because the tester had
to work around the tool rather than with it.

(A sixth — the rate field appearing to lock after an external `hsie.ini` edit —
was investigated by the tester across many cases and did not reproduce. Dropped,
deliberately: no stale-session detection is specced here.)

**The tester could not run their own test through APE.** The rate field caps at
1000 events per 10,000 hours. The blackbox procedure in
`.scratch/umbrella-roadmap/issues/12-diesel-levers-blackbox-test.md` calls for a
`p_dso` of `0.01` per game-second — **360,000 events per 10,000 hours**, over two
orders of magnitude above the ceiling. The tester hand-edited `hsie.ini` instead.
Every player validating APE against a known-good procedure hits the same wall.

**A failed commit can leave a half-patched install.** When the `sh3.exe` write
succeeds and the `hsie.ini` write then fails, APE rolls `sh3.exe` back — but if the
rollback itself throws, that failure is discarded and the player sees only the
original `hsie.ini` error. The install is left with `sh3.exe` patched and
`hsie.ini` not, and APE says nothing. The code comments promise this cannot
happen; nothing enforces the promise.

**A foreign write to `hsie.ini` is silently overwritten.** The PRD asks that *"a
player who patches with a third-party tool between two APE sessions will be
refused rather than silently overwritten"*. `hsie.ini` sites are checked for key
*presence* only, so an edited value is overwritten without a word. The threat
model is narrower than the PRD line implies: **hsieOptions only flips flag bytes in
`sh3.exe` and never writes `hsie.ini`**, so no tool-versus-tool conflict exists on
this file. The real case is a player editing `hsie.ini` by hand — which the tester
did on day one, and which h.sie's own documentation invites.

**An unreadable or foreign-modified site reads as "applied".** Detection returns
true when a site cannot be read at all, and when a site differs from h.sie's
original *in any way* rather than specifically holding APE's values. A site
another mod touched makes APE's checkbox open ticked.

**The tester can only test on one of their installs.** APE refuses KC 2.5
(h.sie `v 1.6B1`). This is correct behaviour, not a defect — ticket 07 scoped v1
to *"OAE 1.5.2 / h.sie `v 1.6E1`, and nothing else"* precisely because the
`v 1.6B1` RE work was unstarted. But it means the beta can only ever be exercised
against a single build, and the tester wants the next version testable on KC.
Admitting a build is gated on RE, not on the patcher.

## Solution

APE stops being the thing the tester has to work around.

The rate field accepts the values a real test procedure needs, so the blackbox
protocol runs through the tool. When a commit cannot be completed, APE either
leaves the install exactly as it found it or tells the player precisely what state
it is in — never both patched and silent. When a site holds something APE did not
write, APE says so instead of assuming. And APE runs on more than the one install
it was derived from.

## User Stories

1. As a tester validating APE, I want the rate field to accept the rate the
   blackbox procedure calls for, so that I can run the documented test through the
   tool instead of hand-editing `hsie.ini`.
2. As a tester, I want rates far above realistic play values to be visibly marked
   as test values, so that I do not mistake a test setting for a shipping one.
3. As a player, I want the rate field's ceiling to be justified by what the damage
   model can express, so that the limit protects me from nonsense rather than from
   inconvenience.
4. As a player, I want a failed commit to leave my install exactly as it was, so
   that a write error never costs me a working game.
5. As a player, I want a rollback that itself fails to be reported in full, so that
   I know my install is inconsistent and which file is in which state.
6. As a player whose install is left half-patched by a failed rollback, I want APE
   to tell me exactly how to recover, so that I am not left guessing.
7. As a player, I want APE to refuse to write when a site holds a value it did not
   write, so that my own edits are not silently discarded.
8. As a player whose `hsie.ini` was hand-edited, I want the refusal to name the key
   and show both values, so that I can decide whether to keep my edit or take
   APE's.
9. As a player who wants APE's value anyway, I want a way to proceed past the
   refusal deliberately, so that the check protects me without trapping me.
10. As a player, I want APE's checkboxes to reflect what APE actually wrote, so that
    a site another mod modified does not read as an APE patch.
11. As a player with an unreadable install, I want APE to say it cannot read the
    install, so that "cannot tell" is never rendered as "patched".
12. As a maintainer, I want the `hsie.ini` verify path to be as strict as the
    `sh3.exe` context-byte path, so that the two stores make the same promise.
13. As a maintainer, I want the scientific-notation rate format proven against
    h.sie's parser, so that the two-digit exponent APE writes is known to work
    rather than assumed.
14. As a maintainer, I want commit atomicity covered by a test that injects a write
    failure, so that the guarantee is enforced rather than commented.
15. As a maintainer, I want each of these fixes to have a test that fails before the
    fix, so that the beta findings cannot silently regress.
16. As a tester with more than one SH3 install, I want APE to work on KC 2.5, so
    that I can validate a build against something other than the one install it was
    derived from.
17. As a tester, I want APE's refusal of an unsupported build to keep naming the
    builds it does support, so that a refusal tells me what to run it on instead.
18. As a maintainer, I want each site re-proven against a real `v 1.6B1` binary
    before that build is admitted, so that the allowlist keeps meaning "proven
    here" rather than "probably fine".
19. As a maintainer, I want KC 2.5 and OAE 1.5.3.7 admitted as separate allowlist
    entries, so that sharing an h.sie version does not imply sharing a build
    identity.
20. As a player on KC, I want APE to tell me when h.sie's own Diesel Damage switch
    is off in my install, so that I know the feature needs enabling upstream before
    APE can touch it.

## Implementation Decisions

**The seam does not move.** `PatchSession` remains the only thing that touches a
file, per its own header contract. Stories 4–12 and 16–20 land entirely inside it.
Only stories 1–3 reach above it, into the desired-state model's rate validation and
the control bound to it. No new seam is introduced except the write-failure
injection point noted under Testing Decisions.

**Rate ceiling: 360,000 events per 10,000 hours.** The old limit of 1000 was a
taste judgement, not a model constraint. 360,000 is the blackbox procedure's
`p_dso = 0.01` per game-second exactly, so the documented test is reachable at the
top of the range and nothing beyond it is needed. The field's upper region is
annotated in the UI as a test range rather than a play range — the shipping
default remains 1.5. Note the ceiling is now five orders of magnitude above that
default, so the control needs a usable way to reach both ends; a linear
step through the range is not it.

**Commit atomicity.** The rollback path stops discarding its own failure. A
rollback that throws produces a distinct, loud outcome that names both files and
their actual states, and tells the player how to recover. The existing
inconsistent-state comment is replaced by the code that makes it true.

**`hsie.ini` verification.** Presence checking is replaced by comparison against
the stored original line, matching what the `sh3.exe` path already does with
context bytes. Sites already carry the complete original line for exact-text
revert (ticket 08), so the data needed is present. A value APE itself wrote must
still verify — the check distinguishes "APE's value" and "h.sie's original" from
"something else", and only the third refuses. Story 9's deliberate override is
part of this decision: since the case being caught is the player's own hand-edit
rather than a rival tool, a refusal they cannot get past would trap them in a state
they created on purpose.

**Applied-state detection.** Detection answers "does this site hold what APE would
write" rather than "does this site differ from the original". Unreadable becomes
its own outcome, distinct from both applied and unapplied, and surfaces as a
refusal to describe the install rather than as a ticked box.

**Admitting KC 2.5.** This is the largest item here and the only one gated on RE
rather than on code. Ticket 08's admission bar requires every site independently
re-proven against a real binary of the version being admitted; for `v 1.6B1` that
work is unstarted. Encouraging prior evidence, not a substitute for it: the flag
table (`0x14EF00`), slider array (`0x14E074`) and version string (`0x14EC00`) sit
at the *same* offsets in both versions, and the one cave surveyed so far is
identical across them — but `.pcode` diverges 1.16–2.87% between versions, so
nothing is assumed.

Two consequences of the identity-hash model (ADR-0007) that shape this: APE keys
on **build**, not h.sie version, so KC 2.5 (`a7b5fc03…`) and OAE 1.5.3.7
(`1b6a5823…`) are two separate allowlist entries despite both being `v 1.6B1` —
admitting one does not admit the other. And KC's slowest telegraph is `AheadSlow
= 0.40` against OAE's `0.20`; ticket 12 already checked the idle cutoff against
that figure (99.09% at 0.40), so the cutoff needs no rework, but this should be
re-confirmed once KC is actually patchable.

Also relevant to story 23: h.sie's own Diesel Damage flag is **off in every install
surveyed**, KC 2.5 included. A tester on KC will need to flip it upstream before
APE offers the feature at all, and APE should say so rather than presenting an
unexplained disabled panel.

**Exponent format.** APE writes the rate as scientific notation with a two-digit
exponent. h.sie's own shipped lines use one digit. The beta did not settle this —
the tester's rate reached `hsie.ini` by hand, not through APE — so it remains
unproven in both directions and needs one live check against a real install before
the beta is called done.

## Testing Decisions

Tests assert on what `PatchSession` reports and what ends up in the files, never on
how it got there. Prior art is `tests/patcher_gui/PatchSessionTests.cs`, which
already opens a fixture install, hands the session a desired state, applies, and
reopens to assert the round trip; these extend it in the same shape and run under
the existing `tests/patcher_gui/run.cmd` harness.

Covered:

- Applying with a rate at and beyond the new ceiling — accepted below, refused
  above, and the refusal names the limit.
- A commit whose `hsie.ini` write fails leaves `sh3.exe` byte-identical to what it
  was before the attempt.
- A commit whose `hsie.ini` write *and* rollback both fail reports failure, names
  both files, and does not claim success. Requires a seam for injecting write
  failure — introduced at the file-write boundary inside the session, the highest
  point that makes the failure reachable without a real read-only disk.
- A site whose `hsie.ini` line was changed by something other than APE is refused,
  and the refusal names the key and shows both values.
- A site holding APE's own value still verifies and applies cleanly — the
  regression this check most risks.
- A site holding h.sie's original verifies and applies cleanly.
- An install that cannot be read reports unreadable, and no patch reads as applied.
- A site differing from the original but not matching APE's value does not read as
  applied.
- A `v 1.6B1` build that is in the allowlist is accepted; one that is not is still
  refused, and the refusal names the supported builds. Admitting KC 2.5 must not
  admit OAE 1.5.3.7 by side effect, nor any other build sharing its h.sie version —
  the test asserts identity keying, not version keying.
- Applying and reverting on a `v 1.6B1` fixture leaves both files byte-identical to
  their pre-APE state, the same round trip already covered for `v 1.6E1`.

Left to manual verification against a real install, because no fixture can prove
it: whether h.sie's parser accepts APE's two-digit exponent. One patch through the
GUI at a rate high enough to produce damage inside ten in-game minutes settles it.

## Out of Scope

- The synthesised test fixture. `tests/patcher_gui/Fixtures.cs` builds a zero-filled
  `sh3.exe` from the manifest and admits its own hash, so the shipped 1.5.2
  allowlist entry is exercised by nothing. The beta's live validation is stronger
  evidence than the fixture would have been, but it is not repeatable in CI. Tracked
  in ADR-0007's "Left open"; not fixed here.
- The unread `min`/`max`/`default` manifest fields, which duplicate bounds
  hard-coded in the window and can drift.
- The two-patch hardcoding throughout the session, window and state model, which
  turns adding a third patch into a scattered edit. Worth addressing when a third
  patch is actually specced, not before.
- `ape.act` deployment, deferred to crush-depth damage per the status notes on
  ADR-0005 and ADR-0006.
- Raising or revisiting the band model, the idle cutoff, or anything else the
  beta confirmed working.

## Further Notes

The hsieOptions threat model was corrected during this conversation, on the
maintainer's stated confidence: hsieOptions only flips flag-table bytes in
`sh3.exe` and never writes `hsie.ini`. This is load-bearing for how story 7 is
weighted — the foreign-write case is a player's own hand-edit, not a tool conflict,
which is why story 9's override belongs with it. The PRD's *"patches with a
third-party tool"* framing is therefore wrong about the mechanism while right about
the requirement; worth correcting in the PRD when it is next touched.

Beta validation record for the identity-hash decision: OAE 1.5.2, both patches
applied and reverted cleanly against a real install, 2026-08-17; KC 2.5 correctly
refused as unsupported on the same day. ADR-0007's "Left open" has been updated to
record both — the refusal is the more interesting half, since it exercises the
allowlist's negative path against a real binary that is genuinely close to a
supported one.

**KC 2.5 sits differently from the other four items and may want splitting out.**
Everything else here is a contained change behind an existing seam with a test that
can fail today. Admitting `v 1.6B1` is RE work under ticket 08's admission bar, it
gates on access to a real KC binary for each site, and it cannot be finished by
editing the patcher. It is specced here because it is beta feedback and belongs
with the rest of the record, but if the other four land first that is a good
outcome, not a partial one. Ticket 07's v1 scope decision is not overturned by this
spec — it is a v1 decision, and this is what comes after.
