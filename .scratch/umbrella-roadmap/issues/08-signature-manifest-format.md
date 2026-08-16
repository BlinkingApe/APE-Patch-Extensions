# Manifest format and the version allowlist

Type: grilling
Status: resolved

## Blocked by

~~09~~ — resolved 2026-08-16. What it settled that bears directly on this
ticket's schema:

- The forbidden-write region (24 `.pconst` addresses) is **hardcoded per h.sie
  version in the manifest**, authored from a real `hsie.ini` at authoring time —
  not parsed live. It functions as a build-time check ("does this site's
  address fall in the clobber list"), not a runtime lookup.
- **`hsie.ini` is a third site kind**, alongside `sh3.exe` and `ape.act`: keyed
  by `ValN=` name match, not byte offset; stores the **complete original line**
  (not just a value) for exact-text revert; no context/field byte split —
  that's an `sh3.exe`-specific mechanism.
- No blanket redirect exists — each site names its own store (`sh3.exe`,
  `ape.act`, or `hsie.ini`) individually. The schema needs a `store` /
  `site-kind` field per patch-field, not a per-patch or per-version default.

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

## Progress (session interrupted 2026-08-16, resume here)

Grilling started, using hull-repair (`docs/re-notes/03-utanker-hull-repair-signature.md`,
a clean single `sh3.exe`-only patch) and diesel-damage §4c
(`docs/re-notes/04-diesel-damage-signature.md`, the messiest known case — 12 writes
across two store kinds in one patch) as the worked examples to check any schema
proposal against. User paused after two term-pinning questions; three of the
ticket's five open questions are untouched.

**Settled, both user-confirmed:**

- **"Patch"** = the user-facing, checkbox-toggleable unit (hull-repair,
  diesel-damage). It groups one or more **sites** and is what "unchecking"
  reverts as a whole.
- **"Site"** = one write location (one address in `sh3.exe`/`ape.act`, or one
  `ValN=` line in `hsie.ini`), tagged with its own store kind.
- **One site = exactly one field, always.** No multi-field sites. Where a GUI
  concept needs several related values (e.g. diesel-damage's lo/hi band pair),
  that's multiple sites belonging to the same patch; cross-field validation
  (`0 < lo < hi < 1`) lives in the GUI/patcher logic, not the manifest schema.
  Confirms diesel-damage is 1 patch containing 12 sites (8 `hsie.ini` sites + 4
  `sh3.exe` sites), each with 1 field.

**In progress, unanswered — resume by asking this exact question:**

- **Versioning shape.** One manifest with a per-site, per-version offset map
  (a site's version-map entry can simply be absent for a version whose offset
  isn't proven yet — the GUI shows that patch disabled+explained on that
  version), vs. one wholly separate manifest file per supported h.sie version.
  Recommendation offered: the per-site map (option A) — matches the evidence
  (offset varies by version, everything else about a site does not) and
  doesn't force copying a whole version's manifest to add one new site.

**Not yet reached — still open from the ticket body:**

- The bar for admitting a new h.sie version to the allowlist.
- Whether `sh3.exe`-kind and `ape.act`-kind sites share one schema shape.
- What stops a stale generated manifest (from `tools/re-static/`) from
  shipping in the C# build.

No manifest schema has been written down yet — only the vocabulary and the
site/patch grouping are decided. Resuming with `/wayfinder .scratch/umbrella-roadmap/map.md`
and picking this ticket (already claimed) should re-enter here rather than
restart the interview.

## Answer

Resumed 2026-08-16 from the interruption above; the remaining four questions
were put to the user one at a time, each with a recommendation, and all four
were confirmed as recommended.

**Schema — two levels, matching the patch/site vocabulary pinned earlier:**

**Patch** (the checkbox-toggleable unit):
- `id`, `display name`
- `h.sie flag dependency` (optional index into h.sie's flag table — read-only
  check per decision A-2/ticket 05; when off, the GUI shows this patch
  disabled+explained rather than writing into inert code)
- list of `sites`

**Site** (one write location, one field, always — per the earlier pinning):
- `id` (stable, unique within its patch)
- `store` / site-kind: `sh3.exe` | `ape.act` | `hsie.ini`
- `field encoding`: `byte` | `float32` | `operand-immediate` | `fixed-rewrite`
  (the three binary value kinds, plus a fourth for wholesale code replacement
  — see below); `hsie.ini` fields are always the ini's own text representation
- `valid range`, `default` — **required for `byte`/`float32`/`operand-immediate`,
  absent for `fixed-rewrite`**
- **context bytes** (invariant, verified before write, never overwritten) —
  shape is kind-specific, see locator below
- **complete original byte run / line** (ticket 02's revert requirement — not
  just enough to verify, enough to reconstruct exactly)
- **locator**, shape depends on `store`:
  - `sh3.exe`: a **per-version offset map** (`{supported version → offset}`).
    A version's entry exists only once proven for that site (see allowlist
    bar below); absent means this site — and therefore its patch, if any
    site is unproven — shows disabled+explained on that version.
  - `ape.act`: a **single build-recorded offset**, not version-keyed — APE
    owns this binary, so there is one build, one offset (ticket 06).
  - `hsie.ini`: a **`ValN=` key name**, not a byte offset at all, plus the
    stored complete original line for exact-text revert (ticket 09).

**Versioning shape: per-site version map (option A), confirmed.** One
manifest, not one file per version. Matches the evidence — offset is the only
thing that varies by version; everything else about a site (encoding, range,
default, store) does not — and means adding one new site never requires
touching every version's copy.

**Version allowlist bar: re-prove every offset, confirmed.** A version is
admitted only after each site's offset is independently re-proven against a
real binary of that version (diff or disassemble, per the map's "static
first" method) before its entry is added to that site's version map.
Verify-before-write stays a runtime safety net, not a substitute for
authoring-time proof — consistent with ticket 09 finding verify-before-write
alone insufficient (it can't detect the `hsie.ini` load-time clobber).

**Fixed-rewrite sites, confirmed.** Ticket 07 flagged that diesel-damage's
21-byte function rewrite at `0x635818` has no user-tunable value at all —
it's a wholesale code swap, not an operand edit — which the schema above
didn't originally cover. Rather than invent a second manifest concept
alongside "site", `field encoding` gains a fourth kind, `fixed-rewrite`: no
valid range, no default, no user-facing value. It's written when the owning
patch is enabled and restored via the site's own stored original byte run
(every site already carries one, per ticket 02's revert requirement) when
unchecked. One site schema stays the single concept; fixed-rewrite is just
the degenerate case with no field to read back or edit.

**Write-target schema sharing: one shared schema, kind-specific locator,
confirmed.** All three site kinds share the common site fields above; only
the locator sub-shape differs by `store`. Keeps one schema to validate and
evolve instead of three parallel structures to keep in sync.

**Stale-manifest guard: build-time regenerate-and-diff, confirmed.** The
manifest generated by `tools/re-static/` stays checked into git as the
reviewable source of truth. The C# build step re-runs the Python generator
and diffs its output against the checked-in file, failing the build on any
drift, before embedding it as a resource — same shape as ticket 01's
"build + tests + config_check verified green" gate.

**Left open, not this ticket's job:** the ADR that reconciles this ticket's
departure from ADR-0003 (fixed offsets + authoring-time signature proof for
the patcher, vs. ADR-0003's runtime signature scanning, which remains right
for `ape.act`'s own live anchors) is still owed — tracked in the map's
"Documentation debt" fog entry, not resolved here.
