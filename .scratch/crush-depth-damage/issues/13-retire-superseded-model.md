# Retire the superseded crush-depth model's artifacts

Type: task
Status: open
Blocked by: none

## Parent

.scratch/crush-depth-damage/map.md

## Question

The old model — three fraction-of-crush-depth bands of flat HP-per-second bleed, with
stock's `CrashSpeed` neutralized so APE owned 100% of below-crush damage — is
abandoned whole. Several artifacts still assert it. Clear them, so nothing in the repo
states a model the map has decided against.

**Dead issues to delete** (superseded outright, not reworked):

- `03-damage-band-decision-function.md` — the pure band-decision seam. The new model's
  pure seam is different in shape (absolute depth → tier → three effect magnitudes),
  and belongs in the rewritten PRD's Testing Decisions, not here.
- `04-ape-cfg-config-loading.md` — `ape.cfg` was killed by umbrella ticket 02;
  constants now live in a magic-marked struct in `ape.act`'s `.data` per ADR-0006.
- `06-sh3exe-patcher-tool.md` — superseded by the C# WinForms patcher (umbrella
  ticket 05).
- `07-jsgme-packaging-and-distribution.md` — JSGME ruled out entirely (umbrella
  ticket 05), and with `CrashSpeed` untouched there is no data edit left to ship.

**Issues to amend rather than delete:**

- `01-re-crush-depth-damage-signature.md` — keep the static-pass write-up, which is
  still valid and load-bearing. Mark the **CDT hunt dead** (the new model never reads
  crush depth) and point its remaining dynamic hunts at tickets 08 and 09.

**Glossary reversal in `CONTEXT.md`:** the **damage window / band** entry currently
defines the concept as "a range expressed as a fraction of crush depth", names three
bands light/medium/heavy, asserts APE owns the entire model with stock's bleed
neutralized, and explicitly says _avoid "tier"_. Every clause of that is now false.
Replace it with a **tier** entry: an absolute depth threshold in metres, configured in
the patcher, two of them, with tier 3 being stock's own untouched crush-depth damage.
The `_Avoid_` line inverts.

**ADR owed:** the reversal from "APE owns 100% of crush-depth damage" to "APE adds
depth-proportional stress above crush depth and hands the catastrophic case back to
stock" is an architectural decision with no ADR. Write one. It should also record why
the multiplier is poll-and-amplify rather than interception — the alongside-not-hooking
principle from ADR-0001 forces it, and the consequence (APE cannot attribute an HP drop
to a cause) is a real limitation a future contributor will otherwise trip over.

Unblocked: none of this depends on the RE hunts. It is bookkeeping that can run in
parallel with them, and doing it early stops the stale artifacts misinforming the
sessions that resolve 08–11.

## Acceptance criteria

- [ ] The four dead issue files deleted.
- [ ] Old issue `01` amended: CDT hunt marked dead, remaining hunts cross-referenced.
- [ ] `CONTEXT.md`'s band entry replaced with a tier entry.
- [ ] An ADR written for the model reversal and the poll-and-amplify limitation.
