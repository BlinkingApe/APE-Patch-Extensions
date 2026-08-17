# Patcher v1 scope: which patches ship first

Type: grilling
Status: resolved

## Blocked by

~~04~~ (reopened and re-resolved 2026-08-15 — it **puts diesel damage back on this
list**, and as the cheapest candidate on it. See the update below; the earlier
note here, that diesel became an `ape.act` runtime feature, is withdrawn).

~~09~~ — resolved 2026-08-16: no candidate is excluded outright. Diesel damage's
rate field routes to `hsie.ini`, its damage-range field routes to `sh3.exe`
`.pcode`; nothing in the v1 candidate list needs a `.pconst` write. This *does*
confirm the three-way write-target count in the update below is final, not
provisional.

~~11~~ — resolved 2026-08-16 by scope pivot, not by proving the gate: the user
called for trusting h.sie's mechanics rather than continuing to chase RE proof
of the surfaced-gate identity (two sessions had hit diminishing returns).

~~12~~ — resolved 2026-08-16, **PASS**. The black-box levers test forced the
damage band to `rand(1.0, 1.0)` in `sh3.exe` `.pcode` and the rate to
`p_ds = 0.01`/game-second in `hsie.ini`, and got a diesel destroyed outright at
~6 in-game minutes — an outcome h.sie's stock first-hit band cannot produce at
all. **Diesel damage is cleared to enter the v1 list on its merits.** Ticket 09's
per-site store routing is now confirmed in play, not just on paper: both write
targets stuck, in one feature, on a real install.

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

---

## Answer — resolved 2026-08-16

**v1 is two `sh3.exe`-side features on one build. `ape.act` is deferred whole.**

### What ships

1. **Hull-repair range** — built **first**, as the tracer bullet. One `sh3.exe`
   operand-range site (`0x633633`, per
   `docs/re-notes/03-utanker-hull-repair-signature.md`), no derived values, no
   INI writer, no flag-dependency gating. It proves
   manifest → verify-before-write → write → read-back end to end on the simplest
   site APE has, so the hard machinery lands on a tool that already works.
2. **Diesel damage** — built **second**. The architecture-heaviest candidate, and
   deliberately not first: 8 `hsie.ini` `ValN` keys (including the **derived**
   `Val10 = p_dso × 0.977237`, recomputed on every write and never user-facing),
   4 `.pcode` immediates (`0x6351B1/B6/C3/CA`), the **21-byte `fixed-rewrite`** at
   `0x635818`, 2 validated GUI fields with live derived stats, and ticket 05's
   **disabled-with-explanation** path (h.sie's Diesel Damage flag is `0x00` in
   every surveyed install, so this path is exercised by default, not as an edge
   case). Sites: `docs/re-notes/04-diesel-damage-signature.md`.

### Write targets: two, not three

**`sh3.exe` + `hsie.ini`. The `ape.act` target is deferred out of v1 entirely.**

The ticket framed this as "is an `sh3.exe`-only v1 a meaningful proof of the
architecture?" — that framing is now wrong in two directions:

- **v1 is not single-target.** Diesel damage alone spans two stores with read-back
  from both, inside one feature panel. Ticket 09's per-site routing, the INI
  writer, and dual-store read-back are all proven in v1. What goes unproven is
  specifically **ticket 06's deploy/patch/delete lifecycle for `ape.act`** — one
  mechanism, not "half the architecture".
- **Including it would gate the release on unfinished RE, not on build effort.**
  The only candidate that exercises the target is crush-depth constants, and
  `.scratch/depth-pressure-stress/issues/01-re-crush-depth-damage-signature.md` is
  still `needs-info`: the static half is done, but **four dynamic hunts** (boat
  pointer chain, movementObj chain, CDT, mission clock) each need a live Cheat
  Engine session with a human diving the boat. That is not work the patcher build
  can absorb.

**Consequence — the LoadLibrary call-site patch also leaves v1.** It is mandatory
only if `ape.act` must load; with no runtime feature shipping, nothing loads it.
That retires ticket 06's ⚠ (does `sh3.exe`'s `LoadLibrary` site tolerate a missing
file?) as a v1 concern — it returns with whichever version ships the first runtime
feature, exactly where ticket 06 flagged it belongs.

### Version allowlist: exactly one build

**OAE 1.5.2 / h.sie `v 1.6E1`, and nothing else.** Every site APE holds was derived
against the `v 1.6E1`-sized variant set, and ticket 12's black-box test was played
on that exact install (backup SHA-256 `51d14ca3…`). Ticket 08's admission bar
requires independently re-proving each site against a real binary of any other
version; for `v 1.6B1` that work is unstarted, so admitting it would gate v1 on RE
rather than on the tool. Any other build gets an "unsupported" refusal and no
writes. This resolves the fog entry's *"which is the play target"* question **for
v1 purposes only** — by shipping for the build APE was actually proven on, not by
determining which one the user prefers.

### Effort shape: one new `.scratch/gui-patcher/`

**A single effort — PRD + numbered issues — covering the C# tool, the manifest
generator, and both v1 features.** Not one effort per feature: ticket 04 already
ruled diesel damage "not an effort of its own", and ticket 03 has shrunk hull
repair to a single manifest field. Neither carries a PRD alone, and splitting them
would strand the tool's own issues in a third effort that both depend on. This is
where the map's destination — *"scoped precisely enough to spin up its own feature
effort"* — is discharged for both goals at once.

**Build it prototype-first.** The first move inside that effort is a
**`/prototype`** session, not implementation: a rough, throwaway C# WinForms shell
— the feature panels, the two diesel input fields with their live derived stats,
and the disabled-with-explanation state — to react to before any write path is
written. Ticket 05 settled the tool's *architecture*, but nothing has yet settled
what it *looks like* to use, and the diesel panel in particular (two fields whose
consequences — mean damage, "two unrepaired hits reach 1.0" percentage — are
illegible from the inputs themselves) is precisely the "how should it look /
behave" case that wants an artifact to react to rather than a spec to read.

### Done bar: validated in play, by the tool

v1 is done when **the shipped patcher — not hand-edited bytes — writes both
features on the real OAE 1.5.2 install, the effects are observed in play, and
unchecking each feature restores bytes identical to `sh3_BACKUP.exe` /
`hsie_BACKUP.ini`**. Ticket 12 proved the *levers* respond when written by hand;
this proves the *tool* writes them correctly, which is a different claim. Byte
verification alone is insufficient — a correct write to the wrong site passes it —
and the rig for the in-play half already exists from ticket 12, so the gate is
cheap. Note the OAE 1.5.2 install is currently **left in ticket 12's test state**
and must be reverted before it can serve as a clean baseline.

### Explicitly deferred to a later version (not out of scope)

`ape.act` deployment + crush-depth constants (blocked on crush-depth issue 01's
dynamic hunts); the LoadLibrary call-site patch; h.sie `v 1.6B1` / OAE 1.5.3.7;
the docking mod (still fog).

### Carried into the new effort rather than decided here

- The **diesel damage-band default** — whether `hi` is capped below `0.5` so two
  unrepaired hits cannot sum to `1.0`. Graduated out of the map's fog: it is
  tuning work for the feature's own scope, and the value is a user-facing field
  whatever default ships.
- The **hull-repair default range** — same character.
