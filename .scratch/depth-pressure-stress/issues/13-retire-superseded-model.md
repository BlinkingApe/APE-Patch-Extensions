# Retire the superseded crush-depth model's artifacts

Type: task
Status: resolved
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md

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

- [x] The four dead issue files deleted.
- [x] Old issue `01` amended: CDT hunt marked dead, remaining hunts cross-referenced.
- [x] `CONTEXT.md`'s band entry replaced with a tier entry.
- [x] An ADR written for the model reversal and the poll-and-amplify limitation.

## Answer

Resolved 2026-08-17.

- Deleted `03-damage-band-decision-function.md`, `04-ape-cfg-config-loading.md`,
  `06-sh3exe-patcher-tool.md`, `07-jsgme-packaging-and-distribution.md`.
- Amended `01-re-crush-depth-damage-signature.md`: CDT hunt and the `CrashSpeed`
  data-file task marked dead; the boat/depth pointer-chain hunts pointed at tickets
  `08`/`09`; static-pass write-up kept as still-valid, still-cited context.
- `CONTEXT.md`: `damage window`/`band` entry replaced with a `depth tier` entry
  (absolute depth, multiplier + seep + wear, tier 3 untouched); `_Avoid_` line
  inverted (was "avoid tier", now "avoid damage window/band"). Also corrected the
  `crush depth` and `AOB pattern scan` entries, which still asserted APE
  reads/anchors CDT.
- Wrote `docs/adr/0008-depth-effects-hand-crush-depth-back-to-stock.md`: records the
  reversal from "APE owns 100% below crush depth" to "tier 3 is stock's, untouched",
  and why the multiplier must be poll-and-amplify (ADR-0001's alongside principle
  rules out hooking the writer to see the delta directly) — including the resulting
  limitation that APE cannot attribute an HP drop to a cause.

**Scope discovered beyond this ticket's original text, not resolved here:**

- `05-ape-act-hook-install-and-wiring.md` was just as stale as `01` — the full
  end-to-end wiring ticket for the dead model, with real linked C++ built against
  it — but wasn't on this ticket's list. Amended in place with a superseded banner;
  left `needs-info` pending the PRD rewrite rather than resolved, since rewiring it
  onto the tier model is implementation work blocked on tickets `08`–`12`.
- The dead model's C++ is still live in the build graph
  (`src/mods/crush_depth_damage/*`, `CMakeLists.txt`, `tools/config_check`,
  `tests/damage_band_test.cpp`, `data/Cfg/ape.cfg`) — deleting it safely needs its
  own session (risk of breaking the build) rather than folding into this pass.
  Spun into new ticket `15`.
