# Patcher v1 scope: which patches ship first

Type: grilling
Status: open

## Blocked by

~~04~~ (reopened and re-resolved 2026-08-15 — it **puts diesel damage back on this
list**, and as the cheapest candidate on it. See the update below; the earlier
note here, that diesel became an `ape.act` runtime feature, is withdrawn).

09 — a feature whose constants sit in `hsie.act`'s runtime-clobbered region
cannot ship as a static patch at all, so 09 decides which candidates are even
eligible for the v1 list.

11 — diesel damage must not enter the v1 list until the surfaced gate is proven;
a negative result redesigns the feature rather than delaying it.

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
- **Diesel damage** — **viable, and the candidate that exercises the most of the
  architecture** (revised 2026-08-15). Eight `ValN` keys in `hsie.ini` **plus**
  five `.pcode` sites in `sh3.exe` (four damage-band immediates and one 21-byte
  function rewrite), **plus** two GUI input fields with validation — i.e. two
  write targets and read-back from both, inside one feature panel. See the update
  at the bottom of this ticket. Still `0x00`/**disabled** in every surveyed
  install, so it ships behind ticket 05's disabled-with-explanation path.
- **Docking mod** — still fog. Not a v1 candidate.

Decide:

- Which of the above ship in v1, and in what order.
- Whether v1 must exercise **both** write targets (`sh3.exe` and `ape.act`) to be
  a meaningful proof of the architecture, or whether an `sh3.exe`-only v1 that
  ships sooner is preferable with the `ape.act` path following.
- Whether the patcher spins up its own `.scratch/<slug>/` effort at v1 scope, or
  whether the architecture alone (from 05) is enough to start building against.

## Update 2026-08-15 — diesel damage returns, and drags a third write target in

Ticket 04's 2nd-pass resolution lands diesel damage here rather than in `ape.act`.
It is cheap in bytes but it is **not free architecturally** — it introduces a
write target this ticket's candidate list did not previously contain:

- **`hsie.ini` becomes a third write target**, alongside `sh3.exe` and `ape.act`.
  The tool needs a **surgical INI writer**: edit named keys in place, preserve
  comments, ordering, and every key it does not own (OAE has customised most of
  the others). This changes the "must v1 exercise both write targets?" question
  above into a three-way one.
- **`sh3.exe` `.pcode` is the second target**, five sites: four damage-band
  immediates (`0x6351B1/B6/C3/CA`) and a **21-byte function rewrite** at
  `0x635818`. The rewrite is the first thing APE has decided to patch that is
  *code* rather than an operand — worth noting when settling how the manifest
  expresses a site, since it has no "mutable field" at all.
- **Two user input fields**, which the user wants specifically for tuning sweeps:
  **rate** (expressed as events per 10,000 h, default 1.5) and the **damage band**
  (`lo`/`hi`, default 0.15/0.55). Validate `0 < lo < hi < 1`, and show the derived
  consequences live — mean damage, and the "two unrepaired hits reach 1.0"
  percentage, neither of which is legible from the inputs.
- **One feature panel spanning two stores.** The rate lands in `hsie.ini`, the
  damage band in `sh3.exe`. Read-back to populate the GUI must come from both.
  This is the concrete case that breaks ticket 02's "the binary is the only
  store" — see ticket 09.

## Notes

Do not re-open the decisions in `05-gui-patcher-scope.md` — this ticket is
strictly about *which patches*, not *how the tool works*.
