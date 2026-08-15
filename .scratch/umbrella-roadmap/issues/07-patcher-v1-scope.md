# Patcher v1 scope: which patches ship first

Type: grilling
Status: open

## Blocked by

04

## Question

Surfaced 2026-08-15 while resolving `05-gui-patcher-scope.md`. The patcher's
architecture is settled (APE-only scope, AOB + verify-before-write, two write
targets, C# WinForms, embedded generated manifest, standalone in the game root).
Its **feature list is not** — and this was deferred deliberately, not overlooked:
the user's call was to *"wait on this, until we know which of our planned patches
are actually viable."*

So this ticket asks: **what ships in v1?**

Viability status of the candidates as of resolution:

- **`ape.act` LoadLibrary call site** (ADR-0002) — required for `ape.act` to load
  at all, so effectively mandatory in any release. Currently unimplemented
  (`src/patcher/main.cpp` is a scaffold).
- **Hull-repair range** — viable. Site, operands, and semantics fully established
  (`docs/re-notes/03-utanker-hull-repair-signature.md`). The natural first
  consumer that proves a h.sie-cave value edit end-to-end.
- **Crush-depth constants into `ape.act`** — viability depends on ticket 06, which
  decides the mechanism. This is the *only* candidate that exercises the second
  write target, so excluding it leaves half the architecture unproven.
- **Diesel damage** — cave located, but **not yet viable**: ticket 04 must confirm
  the chance/amount/timer knobs are all expressible as patchable operands. Ticket
  04 explicitly flags that if any must vary with live game state, that is a
  genuine exception to raise against 05/06. Also note it is `0x00`/**disabled**
  in the live install.
- **Docking mod** — still fog. Not a v1 candidate.

Decide:

- Which of the above ship in v1, and in what order.
- Whether v1 must exercise **both** write targets (`sh3.exe` and `ape.act`) to be
  a meaningful proof of the architecture, or whether an `sh3.exe`-only v1 that
  ships sooner is preferable with the `ape.act` path following.
- Whether the patcher spins up its own `.scratch/<slug>/` effort at v1 scope, or
  whether the architecture alone (from 05) is enough to start building against.

## Notes

Do not re-open the decisions in `05-gui-patcher-scope.md` — this ticket is
strictly about *which patches*, not *how the tool works*.
