# The static patcher drops the build-identity check; hsie version becomes display-only

Supersedes **ADR-0007**'s "Decided" section. ADR-0007's context and scope reasoning
(static files vs. `ape.act`'s live process, verify-before-write as the actual
self-verification payoff) still hold; only the identification mechanism changes.

## Context

ADR-0007 gave APE a whole-file SHA-256 allowlist: a build APE has never seen is
refused outright. In practice this meant one entry per OAE/KC release, because
each release changes bytes elsewhere in `sh3.exe` that APE never touches, which
still changes the file's identity. Two OAE/KC releases were already admitted
([`.scratch/gui-patcher/issues/13-hsie-version-only-build-check.md`](../../.scratch/gui-patcher/issues/13-hsie-version-only-build-check.md))
and both proved to share byte-identical offsets for every site APE owns — the
allowlist was never actually catching a relocated site, only gating on releases
APE hadn't re-certified.

Two facts made the allowlist redundant rather than merely inconvenient:

- **h.sie already gates this.** h.sie's own installer refuses to run its
  hardcode fixes against an `sh3.exe` it does not support. Any install APE
  opens that has a readable `hsie.ini` and hsie version stamp has already
  passed that check.
- **Verify-before-write is the check that actually matters.** ADR-0007 already
  established this as the mechanism that catches a relocated or foreign-edited
  site; the identity hash only ever added a coarser, whole-file version of the
  same guarantee, at the cost of a manifest update on every unrelated release.

## Decided

**The identity hash and its allowlist are removed.** `Site.Offsets`
(`Dictionary<versionId, int>`) collapses to `Site.Offset` (a single `int`) —
there is one proven offset per site, not one per admitted build. `PatchSession`
no longer calls anything like the old `Manifest.Identify`; an install is
`Supported` once `sh3.exe`, `hsie.ini` and `en_menu.txt` are present and
readable, full stop.

APE still reads the h.sie version stamp it always has
(`Manifest.VersionStringOffset`/`VersionStringLength`) and still shows it in
the header. It is now purely informational: never matched against a list,
never grounds for refusal.

**Verify-before-write is unchanged and is now the only safety net.** The
per-site invariant-context-byte check that already ran before every write
(ADR-0007's own point) still runs, still aborts the whole commit on a
mismatch, and is now what catches a build whose sites genuinely have moved —
just at write time instead of at open time.

## Scope

This narrows ADR-0007's "Decided" section only. Its "Context" and "Scope"
sections — static files vs. `ape.act`'s live process, this ADR governing only
the patcher's on-disk edits — still apply unchanged. `docs/re-notes/05-kc25-admission-evidence.md`
and ADR-0007's own "Validated in practice" and "Left open" sections are kept
as history: the evidence that offsets are shared across builds is exactly what
motivated this decision, and the admission-bar prose they reference no longer
describes anything APE does.
