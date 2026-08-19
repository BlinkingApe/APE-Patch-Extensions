# Build check: drop sh3.exe identity, keep hsie version only

Status: ready-for-agent

## Problem Statement

APE currently refuses to run against any sh3.exe it does not recognise by a
whole-file identity hash (`Manifest.Identify`), keyed per admitted build (OAE
1.5.2, KC 2.5, ...). Every new OAE/KC point release changes bytes elsewhere in
sh3.exe that APE never touches, which changes the file's identity and makes
APE refuse a perfectly good install until the maintainer re-proves offsets
against the new build and ships a manifest update. Players on a release APE
hasn't caught up with get turned away for no functional reason, and the
maintainer is stuck re-certifying after every upstream release.

h.sie itself already refuses to install its hardcode fixes against an
sh3.exe it does not support. Any install APE opens that has hsie.ini and a
readable hsie version stamp has therefore already passed that gate — APE's
own whole-exe identity check is redundant with it.

## Solution

Strip the per-build sh3.exe identity check entirely. APE keeps reading and
displaying the hsie version stamp it already reads today, but that string
becomes purely informational — it is never matched against an allowlist and
never gates anything. APE's sh3.exe site offsets, which are already
byte-identical across every currently-admitted build, collapse to a single
proven offset per site rather than one offset per build id. An install is
"supported" once sh3.exe, hsie.ini, and en_menu.txt are present and
readable — nothing about which specific build produced them is checked at
open time. The existing per-site invariant-context-byte check that `Verify()`
already runs before every write remains APE's actual safety net: if a site's
bytes don't match what APE was proven against, the whole commit is refused,
exactly as it is today.

## User Stories

1. As a player on an OAE or KC point release APE has never seen before, I
   want APE to open my install and offer its patches, so that I don't have
   to wait for a manifest update just because unrelated bytes moved
   elsewhere in sh3.exe.
2. As a player, I want APE to still refuse to write if the actual bytes at a
   patch site don't match what APE expects, so that I'm protected from a
   corrupted or genuinely incompatible install even without a build
   allowlist.
3. As the maintainer, I want to stop re-proving and re-shipping the manifest
   every time OAE or KC cuts a release that doesn't touch any site APE
   owns, so that I'm not on the hook for a release cadence I don't control.
4. As a player, I want the header to show the hsie version APE found so I
   can sanity-check my install, without APE claiming to know which OAE/KC
   product build I'm running (a claim it can no longer prove).
5. As a player with a genuinely unsupported install (h.sie itself not
   installed, or sh3.exe otherwise missing/corrupt), I want a clear refusal
   message, so that I know why APE won't proceed — sourced from the
   existing file-presence checks and from `Verify()`'s per-site mismatch
   message, not from a build-identity mismatch.

## Implementation Decisions

- Remove `Manifest.Identify()`, `Manifest.IdentityOf()`, and
  `Manifest.VersionByIdentity()`. Remove `SupportedVersion.IdentitySha256`
  and `SupportedVersion.ReferenceSha256` (the latter was already
  display-only and unused by any UI surface — confirmed dead outside
  `Manifest.cs` and the test fixtures).
- `Site.Offsets` changes from `Dictionary<string versionId, int>` to a
  single `int`. Every site's offset lookup (`Site.OffsetFor`, `ReadRun`,
  the write path in `Commit`) drops the version-id argument entirely.
  Justification: the current manifest already proves OAE 1.5.2 (hsie
  v1.6E1) and KC 2.5 (hsie v1.6B1) share byte-identical offsets for every
  site, so the per-version dictionary has never actually varied in
  practice.
- `PatchSession.Read()` no longer calls `Manifest.Identify()` and no longer
  refuses on an unrecognised build. `Install.Supported` becomes true once
  sh3.exe, hsie.ini, and en_menu.txt exist and are readable (the checks
  already at the top of `Read()` stay; only the identity-match step and its
  refusal are removed).
- `Install.HsieVersion` (already read via `PatchSession.ReadHsieVersion()`)
  stays exactly as it is — this mechanism is not changing, only what
  happens with the value: it is displayed, never matched against a list.
- Remove `Install.BuildName` and `Manifest.SupportedBuildsSentence()`
  entirely, along with the "APE does not recognise this sh3.exe, supported
  builds: ..." refusal string.
- `MainForm.RenderHeader`: drop the build-name segment and the
  `rowExpected`/"expected supported builds" row. Keep the hsie version and
  sha256 in the header row. Reword `verdictSub` — it currently reads "APE
  will only write to a build it recognises" / "APE writes nothing to a
  build it does not recognise", both of which describe the identity check
  being removed; replace with wording describing the remaining checks
  (files present and readable at open time, per-site bytes verified at
  write time).
- `PatchSession.Verify()` is unchanged — it already runs unconditionally
  before every write and already carries the "expected X, found Y, nothing
  was written" per-site refusal. This becomes the only remaining gate
  against a mismatched sh3.exe.
- Bump `Manifest.Schema` (currently `1`) to `2`, since the offsets shape and
  the dropped fields are a breaking change to the manifest JSON format.
  `Manifest.FromJson` should read the new single-offset shape and reject
  schema `1` the way it already rejects any schema newer than it
  understands (symmetric handling, not a special case).
- `tools/re-static/gen_manifest.py`: `exe_site()`'s `"offsets": dict(...)`
  construction collapses to a single `"offset": off(va)` value. The
  `VERSIONS` list, `ALL_VERSION_IDS`, `normalised_hash()`, and the
  identity-matching branch of `verify_against_reference()` are removed.
  `verify_against_reference()` keeps re-reading every site's original bytes
  and the hsie version stamp out of any reference binary under
  `data/exe_with_hsie_patches/`, but does so unconditionally against the
  single offset set rather than per matched version. Regenerate
  `data/manifest/ape-manifest.json` from the updated generator once the
  script changes land, and confirm `--check` passes clean.
- `Manifest.AddVersionSharingOffsets` (test-only helper) is removed — there
  is no longer a versions list to add to. Whatever synthetic-build
  construction the tests need reduces to just building a stock exe with a
  given hsie version stamp; see Testing Decisions.

## Testing Decisions

- `tests/patcher_gui/Fixtures.cs`: `StockBuild`/`Admit` currently register a
  synthetic `SupportedVersion` via `AddVersionSharingOffsets` and stamp
  `IdentitySha256`/`ReferenceSha256`. Simplify to just writing the hsie
  version stamp and site originals into the synthetic exe at the manifest's
  single offsets — no admission step, since there is nothing left to admit
  against.
  `Fixtures.ShippedVersionId` is removed along with it (no version id left
  to look up).
- `TwoBuildsSharingAnHsieVersion()` exists specifically to prove "sharing an
  h.sie version must not imply sharing an allowlist entry" (ticket 01 story
  19) — a claim the new design inverts (sharing an h.sie version, or any
  version, now *does* mean sharing the one offset set, because there is no
  allowlist at all). Remove this fixture and whatever test in
  `PatchSessionTests.cs` consumes it; the property it protected no longer
  exists to protect.
- `UnrecognisedBuild()` currently flips a byte far from every site (`0x1000`)
  to break the whole-file identity hash while leaving every site's bytes
  intact, and asserts APE refuses to open. Under the new design this same
  fixture would no longer produce a refusal at open time (no identity check
  reads that byte, and no site or context lives there), so the existing
  assertion becomes false. Repurpose it (or add a new fixture) to instead
  flip a byte *inside* a site's context region, and assert `Verify()`
  refuses the write with its per-site mismatch message — this is the
  behavior that actually replaces the old refusal path, and it should be
  tested at the seam that now owns it (`PatchSession.Commit`/`Verify`, not
  `PatchSession.Read`).
- Add or extend a `PatchSessionTests.cs` case asserting that a stock install
  stamped with a novel/arbitrary hsie version string (one that appears
  nowhere in the manifest) is still `Install.Supported == true` and every
  patch still reads and applies normally — this is the direct positive
  test of "any hsie version is accepted."
- Prior art for all of the above: the existing `PatchSessionTests.cs` /
  `Fixtures.cs` pair is the seam — same style of stock-install fixture,
  same assertion shape against `Install`/`PatchState`, no new test
  infrastructure needed.
- After the C# changes, run `python tools/re-static/gen_manifest.py --check`
  to confirm the regenerated manifest matches what's checked in, and run
  the full `tests/patcher_gui/run.cmd` suite.

## Out of Scope

- Any change to how patch *sites themselves* are located, verified, or
  written (`Verify()`'s context-byte mechanism, `TargetRun`/`TargetLine`,
  the write/rollback sequence in `Commit`) — only the build-identification
  layer above it is being removed.
- Adding new admitted builds, or re-proving offsets against any binary not
  already covered by the current manifest.
- Any change to how h.sie's own version stamp is written or where APE reads
  it from (`VersionStringOffset`/`VersionStringLength`) — only what APE
  does with the value once read is changing.
- Re-litigating whether a per-build identity check should exist at all in
  some future, different form (e.g. an opt-in strict mode) — this ticket
  removes it outright, per the decision above.

## Further Notes

Grilled 2026-08-19 (no wayfinder map needed — the two open questions this
required were resolved directly: accept any hsie version with no allowlist,
and collapse the per-version offsets dictionary to one universal offset set,
both backed by the existing manifest already showing identical offsets
across the two admitted builds today). Filed as a follow-on to the v1.1
scope map rather than into it, since v1.1 is a closed scope and this is new,
separately-motivated work (user-reported friction + maintenance cost, not a
v1.1 polish item).
