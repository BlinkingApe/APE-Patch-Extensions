# ADR: reconcile the GUI patcher's fixed-offset model with ADR-0003

Type: grilling
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

The PRD's Further Notes flags this as owed: an ADR reconciling the GUI
patcher's authoring-time, fixed-offset proof model (offsets proven once via
`tools/re-static/`, embedded in the manifest, checked at runtime only against
invariant context bytes) with ADR-0003's runtime signature scanning, which
remains the correct approach for `ape.act`'s live anchors.

Resolve via `/domain-modeling`: what's the actual boundary between "fixed
offset is legitimate" (this tool, proven per-build via identity hash) and
"runtime scanning is required" (`ape.act`, which must find its anchors live)?
Write the ADR once that boundary is clear.

Is this genuinely a v1.1-scope item, or purely documentation debt that can
land whenever, independent of what else ships in v1.1? Settle that as part
of resolving this ticket — it may turn out to be worth doing regardless of
the v1.1 release boundary, in which case note that on resolution rather than
holding it hostage to the rest of the release.

## Answer

Resolved 2026-08-18. **The ADR isn't owed — it already exists.**

`docs/adr/0007-static-patcher-verifies-by-build-identity-hash-not-aob-scan.md`
(dated 2026-08-17, predating this ticket) is exactly the reconciliation this
ticket asked for: it states the boundary plainly under "Scope" —
ADR-0003 still governs `ape.act`'s runtime resolution of *live* values
(process running, addresses move, a scan is the only honest option);
ADR-0007 governs the patcher's static edits to files on disk (`sh3.exe`,
`hsie.ini`, and future `ape.act` per ADR-0006), where the image is knowable
in advance and identified by whole-file identity hash instead. ADR-0003
itself already carries a scoping note at its top pointing to ADR-0007. The
PRD's "an ADR is still owed" line is stale — the work was already done.

**Genuine leftover found while verifying this:** ADR-0007's own "Scope"
section flags that `CONTEXT.md`'s glossary — "APE still resolves its value
anchors dynamically rather than trusting fixed addresses" (line 8) and the
**AOB pattern scan** entry's "_Avoid_: hardcoded offset" (lines 23-25) —
is written as a project-wide absolute and now overclaims, since the static
patcher legitimately uses fixed offsets. ADR-0007 left the reword as an
undone follow-up. **In scope for v1.1** (a correctness fix to a doc
contributors rely on, not just documentation debt) — spun off as
[ticket 11](11-context-md-runtime-anchor-wording.md).
