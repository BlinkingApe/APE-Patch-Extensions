# The static patcher verifies by build-identity hash and fixed offsets, not AOB scanning

**ADR-0003** decided that APE resolves its anchors by AOB signature scan rather than
hardcoded offsets, and **CONTEXT.md**'s glossary states the rule flatly: *"APE still
resolves its value anchors dynamically rather than trusting fixed addresses."* Written as
stated, that rule covers everything APE touches. The GUI patcher breaks it — `Site.Offsets`
plus `OffsetFor(versionId)` are exactly the hardcoded offsets ADR-0003 bars.

That is deliberate, and this ADR records why rather than leaving the contradiction to a
code comment.

## Context

ADR-0003 was written when APE's only mechanism was a runtime `ape.act` following pointer
chains to *live* values in a *running* process. Two of its premises don't hold for a static
patcher:

- **Nothing is running.** The patcher edits files on disk before launch. There is no
  process memory to scan, no ASLR to defeat, and no pointer chain to root. The thing it
  needs to find is a byte offset in a file it can hash end to end.
- **The image is knowable in advance.** Ticket 05 (`05-gui-patcher-scope.md`) dropped
  runtime AOB scanning for the patcher in favour of a **version allowlist + version-keyed
  fixed offsets + verify-before-write**; ticket 08 (`08-signature-manifest-format.md`) then
  settled the manifest schema on that basis. ADR-0006 already assumes the outcome —
  *"No runtime AOB scanning, consistent with ticket 05's dropping of that technique in
  favour of build-time-known offsets plus verification"* — but no ADR ever recorded the
  decision itself. This one does.

## Decided

**The patcher identifies its target by whole-file SHA-256 against an allowlist of known
builds, then trusts the offsets recorded for that build.** A build APE has never seen is
refused outright rather than patched on a guess. Offsets are authored per version into
`data/manifest/ape-manifest.json` by `tools/re-static/gen_manifest.py` and shipped embedded
in the patcher exe.

This preserves ADR-0003's actual payoff — self-verification, so a shifted anchor is
*detected* rather than silently misread — through a different mechanism suited to static
files. An AOB scan detects drift by missing; an identity hash detects it by not matching,
and detects it more strictly: any byte change anywhere in the image, not just at the anchor,
takes the build out of the allowlist. The fail-inert posture is unchanged in spirit, though
the failure is louder: the patcher refuses and says so, because unlike `ape.act` it has a
user standing in front of it who can act on the message.

**Verify-before-write remains mandatory on top of the hash.** The hash establishes *which
build this is*; the per-site context-byte check (ticket 02's context/field split)
establishes *that this site still looks the way the manifest says*, and catches an install
a third-party tool has touched since. Identity hashing does not replace that check.

## Scope

This narrows ADR-0003 rather than superseding it. ADR-0003 still governs **`ape.act`'s
runtime resolution of live values** — player boat, depth, crush depth, HP, mission clock —
where the process is running, the addresses move, and a scan is the only honest option.
This ADR governs **the patcher's static edits to files on disk** (`sh3.exe`, `hsie.ini`,
and in future `ape.act` per ADR-0006).

CONTEXT.md's *AOB pattern scan* glossary entry and its "Avoid: hardcoded offset" note are
written as project-wide absolutes and now overclaim; they need rewording to say *runtime
anchors* specifically. Left as a follow-up, not done here.

## Validated in practice (2026-08-17)

Beta testing against real installs exercised both paths of the identity check:

- **Positive.** OAE 1.5.2 (`identitySha256: 36d66176…`) was recognised, both patches
  applied, and unchecking them restored stock behaviour.
- **Negative.** KC 2.5 (h.sie `v 1.6B1`, `a7b5fc03…`) was refused as unsupported with no
  writes — the case that matters most, since KC shares h.sie's flag-table, slider-array and
  version-string offsets with the supported build and would likely have *appeared* to patch
  correctly under a looser check.

## Left open

The allowlist is still exercised by no *automated* test: `tests/patcher_gui/Fixtures.cs`
synthesises a zero-filled `sh3.exe` from the manifest and admits its own hash via
`Manifest.AddVersionSharingOffsets`, so the fixture is built from the same offsets it would
validate. The manual results above are stronger evidence than that fixture would have been,
but they are not repeatable in CI and do not survive a refactor.

Admitting further builds is governed by ticket 08's admission bar (every site re-proven
against a real binary of that version), not by this ADR. Note that the bar is per **build**,
not per h.sie version: KC 2.5 and OAE 1.5.3.7 are both `v 1.6B1` but hash differently, so
admitting one leaves the other refused. See
`.scratch/gui-patcher/issues/01-beta-feedback-fixes.md`.
