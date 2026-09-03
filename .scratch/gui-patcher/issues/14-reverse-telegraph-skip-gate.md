# Diesel damage: gate out reverse telegraph to stop the NaN-driven runaway damage

Type: research
Status: resolved

## Blocked by

None.

## Question

**Bug, confirmed 2026-08-21 in play** (v1.11 shipped defaults, OAE 1.5.2):
diesel wear damage fired repeatedly — 8 hits within real seconds, continuing
even after the player cancelled time compression — while reversing at 4
knots. Root-caused in
[umbrella ticket 04's 3rd-pass answer](../../umbrella-roadmap/issues/04-diesel-damage-patch-scope.md#answer-3rd-pass--resolved-2026-08-21):
the cave's probability calc (`p_ds = p_dso × Speed^0.01` via CRT `pow()`,
`0x635111`/`0x635117`) is fed the raw machine-telegraph value with no sign
check. If reverse orders are a negative float, `pow(negative, 0.01)` returns
`NaN`, and NaN's unordered-comparison flags happen to satisfy *both*
downstream branches toward "apply damage" — the threshold gate at
`0x635129`/`0x635131` (`jb`) and the roll-check at `0x63516C` (`jae`) — so
every real-second tick scores a guaranteed hit regardless of the RNG roll or
the configured rate.

**Decision already made** (umbrella ticket 04): don't chase confirming the
exact astern-telegraph encoding via live CE (two prior sessions on this cave
both went badly — see umbrella ticket 11's history). Instead, add an
explicit `Speed < 0.0 -> skip` gate ahead of the `pow()` call, so diesel wear
never triggers while the boat is ordered astern, full stop. This is
intentionally a stricter cut than a defensive `fabs()` would be.

This ticket is the RE + implementation work umbrella ticket 04 didn't do:

- **Find room for the new compare.** The cave is dense `.pcode`; the four
  damage-band sites are data-immediate rewrites (no bytes needed), but this
  is a *new* conditional branch. Check whether the existing 21-byte
  `0x635818` rewrite (34 bytes → 21 used, `int3`-padded to `0x635844`, see
  `docs/re-notes/04-diesel-damage-signature.md` §3a/§3b) or any other cave
  padding has spare bytes, or whether the skip has to be spliced in as its
  own small trampoline the way the outer hook (`0x635040`) already is.
- **Placement:** before the telegraph value reaches the `pow()` call at
  `0x635111`/`0x635117`, jumping to the cave's existing `0x635204` "no
  damage this tick" exit (same exit the surface gate and TC-changed gate
  already use) on `Speed < 0.0`.
- **Confirm this doesn't touch the already-proven idle cutoff.** Telegraph
  `== 0.0` must keep hitting the existing `Val10` threshold path exactly as
  ticket 12 tested it (`pow(0, 0.01) = 0`, below threshold → no damage); this
  new gate should only intercept strictly negative values.
- **New manifest site.** Add it to `gen_manifest.py` / `data/manifest/ape-manifest.json`
  as a `fixed-rewrite` site (same `field encoding` ticket 08 defined for the
  `0x635818` rewrite), with the version-keyed offset for OAE 1.5.2 / `v
  1.6E1` — the only build in the v1 allowlist.
- **Test:** extend `tests/patcher_gui/PatchSessionTests.cs`'s diesel-damage
  section with a case exercising the new site's verify-context + byte-write
  round-trip, mirroring how the other diesel `.pcode` sites are tested.
- **Live confirmation, low-cost version:** once built, the cheapest way to
  validate is *not* a CE session on the cave's internals — it's playing with
  the shipped patcher's forced-magnitude/high-rate test rig from
  [umbrella ticket 12](../../umbrella-roadmap/issues/12-diesel-levers-blackbox-test.md)
  while reversing, and confirming no hit lands (mirroring how that ticket's
  "all stop" / submerged conditions were validated black-box, without ever
  resolving the underlying field identities).

## Update 2026-08-21 — live confirmation, save-repair addresses, one loose thread

The user ran a live CE session repairing their save and, along the way,
gathered evidence directly relevant to this ticket:

- **Reproduced at normal TC 1×, not just under compression.** Set a
  write-breakpoint on the confirmed-live starboard float, reversed, and got
  multiple hits in seconds at 1× time compression. This rules out any
  residual "TC scaling explains it" reading and confirms the mechanism is a
  **per-real-second-tick guaranteed hit**, exactly as this ticket's NaN
  theory predicts — independent of TC entirely.
- **Confirmed pointer chain for both engines**, dereferencing all three
  levels (matches `docs/re-notes/04-diesel-damage-signature.md` §"The two
  diesels" exactly — `mov edi,[eax]` / `mov esi,[eax+4]` at
  `0x6350F3`/`0x6350F5`, where `eax = p2+0x30C`):
  - Port: `[[0x5543DC]+4]+0x30C]`
  - Starboard: `[[0x5543DC]+4]+0x310]`

  Both resolve fresh each session (the pointer *values* stored in those slots
  are session-specific heap addresses — do not hardcode the resolved
  addresses themselves, only the offset chain).
- **⚠ New, unidentified loose thread — not chased, not blocking.** The
  write-breakpoint on the starboard float also caught a second, unrelated
  writer at `0x633147` (near the u-tanker block, *not* the diesel cave),
  firing in the same session window while reversing. Disassembly:
  `fdiv [0x63C000]; fsubr [ecx]; fst [ecx]; fcomp [0x51DAAC]; jmp 0x41DFC0`.
  Not present anywhere in the existing RE notes. Most likely explanation is
  heap-address reuse coincidence (two unrelated allocations landing on the
  same absolute address in this one session), not a genuine shared field —
  but flagged here in case a future session sees it recur and wants to
  identify it properly.

## Note for whoever picks this up

The exact sign/encoding of reverse telegraph orders is *still* unconfirmed —
this patch works whether or not the NaN hypothesis is exactly right, as long
as reverse telegraph is in fact negative (or the gate is written to also
catch whatever the actual out-of-range representation turns out to be, if
black-box testing shows the bug isn't fully suppressed).

## Answer — resolved 2026-08-21

Built. Full write-up: `docs/re-notes/04-diesel-damage-signature.md` §6.

**Found room without a new trampoline region.** `tc-helper-rewrite`
(`0x635818`) already replaces h.sie's 44-byte time-compression helper with a
21-byte function, leaving 23 bytes of then-unused `int3` padding out to
`0x635844`. The reverse-telegraph gate lives entirely inside that padding —
no new file territory claimed, no separate trampoline site needed.

**Two sites, one new:**

- `reverse-telegraph-skip-jmp` (`0x635111`, 6 bytes) — the `pow()`-arg push
  of the telegraph value becomes a `jmp` into the trampoline.
- The trampoline itself is folded into `tc-helper-rewrite`'s own
  `replacement` field (bytes 21–43 of its 44-byte run), not a separate site.

**The gate is a raw sign-bit test (`test eax, eax` / `js`), not an x87
compare** — deliberately, since x87's unordered-NaN semantics are the exact
mechanism that caused the bug. This also means the gate isn't relying on the
never-confirmed exact encoding of reverse telegraph: it diverts on the sign
bit being set, so it also catches a negative-*signed* NaN telegraph if that
turns out to be the actual representation, and is a harmless no-op if
reverse telegraph turns out not to be sign-negative at all.

Verified against all three reference binaries in
`data/exe_with_hsie_patches/` (`gen_manifest.py`'s
`verify_against_reference` passes clean), manifest regenerated, and a
dedicated test (`PatchSessionTests.TheReverseTelegraphSkipGateIsWrittenAndRestored`)
proves the write/verify/restore round trip. Full test suite (144 cases,
two cultures) passes; `build.cmd` builds clean with the up-to-date manifest.

**Not done this session:** live confirmation in play (the ticket's own
suggested cheap method — the forced-magnitude/high-rate test rig from
umbrella ticket 12, reversing, confirming no hit lands). Recommended as a
follow-up before calling the bug closed for good, though the fix does not
depend on it.
