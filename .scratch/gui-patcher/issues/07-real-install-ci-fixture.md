# Can a real OAE 1.5.2 binary back the CI fixture?

Type: research
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

Ticket 01's Out of Scope: `tests/patcher_gui/Fixtures.cs` builds a
zero-filled synthetic `sh3.exe` from the manifest and admits its own hash, so
the shipped OAE 1.5.2 allowlist entry is exercised by nothing real in CI. The
beta's live validation is stronger evidence than the fixture, but it isn't
repeatable in CI.

Investigate whether a real `sh3.exe` (or a legally/practically viable
derivative of one — e.g. only the bytes at proven sites, if that's sufficient
to exercise the identity-hash and context-byte checks) can be checked into
the repo or fetched at test time, given:

- Redistribution: is a full copy of a commercial game's exe something this
  repo can hold, even for testing?
- Whether a partial/redacted binary (only the manifest's site regions,
  padded) would still exercise the real identity-hash logic meaningfully, or
  whether the hash check specifically requires the whole file.
- Whether this is worth doing for v1.1 at all versus staying flagged as a
  known gap (it doesn't block shipping — the beta already validated the real
  build once, manually).

## Notes

Research ticket — produce a written recommendation (do it / don't, and how)
as a linked asset, not code.

## Answer

Resolved via `/research` 2026-08-18. Full findings:
[research/07-ci-fixture-real-binary.md](../research/07-ci-fixture-real-binary.md).

**Don't pursue it — accept as a known, documented gap.**

1. **Redistribution: no**, and this isn't even a new call — the repo already
   has a settled policy against it. `.gitignore:7-10` excludes
   `/data/exe_with_hsie_patches/` with a comment that commercial game
   executables "do not belong in the repository," and ADR-0005 exists
   specifically to avoid ever redistributing modified copyrighted binary
   bytes. No game binary has ever been checked in.
2. **Partial/redacted binary: technically incapable, confirmed.**
   `Manifest.cs`'s `IdentityOf` (155-172) clones the *entire* file, resets
   only APE's own sites and h.sie's flag table, and hashes the whole cloned
   array — every non-site byte flows into the hash. A partial file with real
   bytes only at proven sites would differ from the real file everywhere
   else and could never reproduce the recorded `identitySha256`. There is no
   partial-file shortcut.
3. **Worth doing for v1.1: no.** The only binary that could exercise the
   real hash (the full file) can't legally be held by the repo; the only
   legally-safe substitute (a partial file) can't exercise that hash logic
   at all. No middle path exists. Doesn't block shipping — the beta already
   validated the real build by hand once, and the synthetic fixture already
   exercises the allowlist mechanism honestly (admits its own computed hash
   the same way `IdentityOf` computes real ones).
