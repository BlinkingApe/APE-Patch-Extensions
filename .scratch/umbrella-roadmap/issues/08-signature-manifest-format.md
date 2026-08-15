# Manifest format and the version allowlist

Type: grilling
Status: open

## Blocked by

None

## Question

Surfaced 2026-08-15 while resolving `05-gui-patcher-scope.md`, then **substantially
narrowed the same day by that ticket's amendment**. It originally asked for an AOB
signature schema plus a definition of "uniqueness proof". Runtime AOB scanning has
since been dropped in favour of a **version allowlist + version-keyed fixed offsets
+ verify-before-write**, so most of that question is void.

What remains:

- **Manifest schema.** What does one patch definition hold? At minimum: a stable
  id, display name, the h.sie flag index it depends on (for the read-only
  dependency check, decision A-2), and per supported h.sie version — the file
  offset, the field encoding (byte / float32 / operand-immediate), valid range,
  and default.
  **Two requirements added by ticket 02 (resolved 2026-08-15):**
  1. Each site splits into **invariant context bytes** (verified before write) and
     the **mutable field** (read back to populate the GUI, overwritten on save).
     Verification checks the context, never the field — that is what lets a
     user-edited value coexist with verify-before-write.
  2. Each site must carry its **complete original byte run**, not just enough
     context to verify. Unchecking a patch is the only revert mechanism, and it
     must reconstruct the exact pre-APE state.
- **How versions key the offsets.** One entry per patch with a per-version offset
  map, or a whole manifest per version? The evidence so far is that offsets are
  *identical* across `v 1.6B1` and `v 1.6E1` for the one cave we have, so a
  per-version map may be mostly redundant — but `.pcode` diverges 1.16–2.87%
  between versions, so it cannot be assumed away.
- **The bar for adding a version to the allowlist.** Currently `v 1.6B1` and
  `v 1.6E1`. What must be true before a new version is admitted — every offset
  re-proven against a real binary of that version, or is verify-before-write
  considered sufficient to admit it optimistically?
- **Do the two write targets share a schema?** `sh3.exe` entries are version-keyed
  and h.sie-flag-dependent; `ape.act` entries are neither (APE owns that binary,
  and ticket 06 decides how constants are located in it).
- **The generation step.** The Python tooling in `tools/re-static/` emits the
  manifest; it is checked in and embedded as a C# resource at build. What stops a
  stale manifest shipping?

## Notes

The AOB work is **not** gone, just relocated: signature matching is now a
Python authoring-time technique for *proving* an offset holds across the supported
build set — exactly the check run in `docs/re-notes/oae-builds.md`
("Cross-build stability") — rather than something the C# patcher does at runtime.

Directly implements ADR-0003 on the static side, and now *departs* from it —
ADR-0003 commits APE to signature scanning over fixed offsets, which remains right
for `ape.act`'s live anchors but has been overruled for the patcher. That
distinction needs an ADR of its own.

Use `/domain-modeling` — "site", "field", "patch", and now "supported version"
need pinning as terms before the schema can be argued about.
