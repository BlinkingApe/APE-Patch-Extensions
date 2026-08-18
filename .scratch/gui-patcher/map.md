# v1.1 scope map

Label: wayfinder:map

## Destination

A finalized, implementation-ready scope for APE Patch Extensions v1.1 — every
open design question answered and recorded as a ticket a builder can pick up
directly. This map does not build v1.1; it decides what v1.1 contains and how
each item should work.

## Notes

Domain: the C# GUI patcher (`.scratch/gui-patcher/`), the tool built and
beta-tested per [ticket 01](issues/01-beta-feedback-fixes.md). Consult that
ticket and the [PRD](PRD.md) for how the existing tool is built — this map
only covers what's new for v1.1.

Naming: the other h.sie `v 1.6B1` build not yet admitted is **OA 1.5.3.7**
(the original OneAlex mod), not OAE — corrected 2026-08-18. OAE refers to a
different install (OAE 1.5.2 / 1.5.3.7 is wrong; the admitted build is OAE
1.5.2, the not-yet-admitted one is OA 1.5.3.7). Don't conflate the two across
tickets.

Skills: `/grilling` and `/domain-modeling` for design-decision tickets;
`/research` for the CI-fixture feasibility ticket.

## Decisions so far

- [Version numbering and display scheme](issues/03-version-numbering-and-display.md) —
  bare `1.1`, source of truth a `const` in a new `Version.cs` read by both
  the header and `build.cmd`, surfaced in both the window header and the
  exe filename (`ApePatchExtensions-1.1.exe`), no version-bump build guard.
- [Hull repair default range: 0-5%, and what changes with it](issues/04-hull-repair-default-range.md) —
  0-5 needs no validity special-casing, but hull repair must adopt diesel's
  conditional-readback pattern in `PatchSession.ReadBack()` (unapplied shows
  APE's default, applied reads the live roll) since simple constant-only
  change would be a no-op today; review wording unchanged; no-op test
  rewritten to construct h.sie's stock 5-20 explicitly.
- [Exponent format: live check against a real install](issues/05-exponent-format-live-check.md) —
  PASS. Live-tested 2026-08-18: h.sie's parser accepts APE's two-digit
  exponent (`4.1667e-08`-style) as written; no fix needed.
- [Diesel damage message: reword and move from pre-damage warning to
  post-damage report](issues/09-diesel-damage-report-message.md) — no
  relocation needed (the stock call already runs after the damage write);
  new text "Diesel trouble, Kaleun. Normal wear and tear, we'll get her
  running, kein Stress."; fires unconditionally now, including on the rare
  destruction tail. String-sourcing mechanism split out to
  [ticket 10](issues/10-diesel-message-string-source.md). Built 2026-08-18:
  a new `SiteStore.MenuText` for a keyed line in `data/Menu/en_menu.txt`,
  plus a fixed-rewrite site nopping the `je` gate at `0x6351F4`. Tests in
  `tests/patcher_gui/PatchSessionTests.cs` section 10.

- [Manifest bounds wiring](issues/06-manifest-bounds-wiring.md) — wire up
  `min`/`max` from the manifest into the UI widgets (closes the drift gap
  and a latent hull floor/width bound mismatch); leave `default` unread
  (stays a stock-only provenance field, not a place for APE's product
  opinion); in scope for v1.1, doesn't touch the two-patch hardcoding
  cleanup.

- [Can a real OAE 1.5.2 binary back the CI fixture?](issues/07-real-install-ci-fixture.md) —
  no: the repo already has a settled no-redistribution policy (`.gitignore`,
  ADR-0005), and the identity hash covers the entire file, so even a
  partial/redacted binary couldn't reproduce it. Stays a documented, known
  gap; doesn't block shipping.

- [ADR: reconcile the GUI patcher's fixed-offset model with ADR-0003](issues/08-adr-fixed-offset-vs-adr0003.md) —
  not owed: ADR-0007 already reconciles it (runtime-live vs. static-on-disk
  boundary), the PRD's "still owed" line was stale. Left CONTEXT.md's
  glossary overclaiming project-wide "resolves anchors dynamically" as an
  undone ADR-0007 follow-up, spun off as
  [ticket 11](issues/11-context-md-runtime-anchor-wording.md), in scope for
  v1.1.

- [Where does the new diesel damage message's text get sourced from?](issues/10-diesel-message-string-source.md) —
  `data/Menu/en_menu.txt`, ID `4893=`, referenced exactly once anywhere
  (confirmed by live disassembly + the real install), safe to overwrite in
  place. A genuine third site type alongside `Sh3Exe`/`HsieIni` — a keyed
  text-file line, same capture/write/revert round-trip as `hsie.ini`
  sites. Encoding is moot; the decided text is pure ASCII.

- [Reword CONTEXT.md's runtime-anchor wording](issues/11-context-md-runtime-anchor-wording.md) —
  done: both glossary passages now scope "resolves anchors dynamically" /
  "avoid hardcoded offset" to `ape.act`'s runtime resolution specifically,
  pointing at ADR-0007 for the GUI patcher's separate fixed-offset model.
  Documentation only, no behavior change.

## Not yet specified

- What v1.2 (or later) picks up from whatever doesn't make v1.1 — not
  sketched until this map's tickets reveal what's left over.
- The actual implementation work for each decided item — deliberately not
  ticketed here; this map hands off a scope, not a build.

## Out of scope

- **OA 1.5.3.7 admission.** RE-gated (needs a real binary, full site-by-site
  re-proof per ticket 08's admission bar), not a code/UX decision — a
  different shape of work than the rest of v1.1. Confirmed out of scope for
  this effort 2026-08-18.
- **`ape.act` deployment.** Deferred to the depth-pressure-stress /
  crush-depth work, per [[session-handoff-build-start]] and the status notes
  on ADR-0005/ADR-0006. Not v1.1.
- **The docking mod.** Still fog elsewhere, unrelated to this tool's v1.1.
- **Two-patch hardcoding cleanup.** Ticket 01 explicitly scoped this to "when
  a third patch is actually specced" — no third patch is specced, so it stays
  out.
