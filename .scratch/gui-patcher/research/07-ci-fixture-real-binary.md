# Can a real OAE 1.5.2 binary back the CI fixture?

**Verdict: Don't.** A full binary can't legally be checked in, and a
partial/redacted binary can't produce a real identity hash anyway — the hash
covers the entire file. Leave this as a known, accepted gap; the beta's
one-time manual validation is the best evidence available and CI should keep
exercising the allowlist mechanism honestly via the synthetic fixture, as it
already does.

## 1. Redistribution

The repo has already decided this question for itself, independently of this
ticket:

- `.gitignore:7-10` explicitly excludes `/data/exe_with_hsie_patches/` with
  the comment: *"they are commercial game executables and do not belong in
  the repository."* This is existing, working policy, not a hypothetical.
- ADR `docs\adr\0005-distribution-via-patcher-tool-not-repatched-exe.md:13`:
  the whole distribution design exists specifically to "avoid ever
  redistributing modified copyrighted binary bytes." Ticket
  `.scratch\umbrella-roadmap\issues\05-gui-patcher-scope.md:228` reaffirms
  that "never redistribute modified copyrighted binary bytes" rationale
  survives later rescoping.
- `tests\patcher_gui\Fixtures.cs:3`: "APE's real target is a 1.4 MB commercial
  binary that cannot be checked in" — stated as fact by the test suite's own
  author-comment, driving the synthetic-fixture design.
- Precedent check: `git ls-files | grep -iE '\.(exe|dll)$'` returns nothing —
  no binary game file has ever been checked in anywhere in this repo, at any
  point. The only third-party binaries under version control are
  `third_party\minhook`'s BSD-licensed source (compiled locally), not game
  assets.
- No root `README.md`, `LICENSE`, or `CONTRIBUTING` file exists to add a
  redistribution stance to — the stance already lives in the `.gitignore`
  comment and ADR-0005 above.
- `git remote -v` returns no output — this working copy has no configured
  remote, so its public/private hosting status can't be confirmed from repo
  state. It doesn't matter for the verdict: distributing a copyrighted
  commercial executable without Ubisoft's permission is copyright
  infringement regardless of whether the host is public or private; a
  private/internal repo only changes exposure, not legality. The existing
  policy correctly treats this as a hard no rather than a risk to size up.

**Conclusion: no**, a full `sh3.exe` copy cannot be checked into this repo,
consistent with policy the project already enforces elsewhere.

## 2. Would a partial/redacted binary even exercise the real hash?

Read `src\patcher_gui\Manifest.cs:151-173` (`IdentityOf`) directly — the
hypothesis is confirmed exactly:

```
155  public string IdentityOf(byte[] exe, string versionId)
156  {
157      byte[] copy = (byte[])exe.Clone();
158      foreach (Patch p in Patches)
159          foreach (Site s in p.Sites)
160          {
...
165              Array.Copy(s.Original, 0, copy, at, s.Original.Length);
166          }
167      if (FlagTableOffset + FlagCount > copy.Length) return null;
168      for (int i = 0; i < FlagCount; i++) copy[FlagTableOffset + i] = 0;
170      using (SHA256 sha = SHA256.Create())
172          return Hex.ToString(sha.ComputeHash(copy));
```

`copy` is the **entire cloned file** (line 157 clones all of `exe`). Only
APE's own site bytes (line 165) and the h.sie flag table (line 168) get
normalized to known values; every other byte — all game code/data untouched
by APE — passes through unchanged into the SHA-256 input at line 172. `Identify`
(line 176-186) just calls `IdentityOf` per candidate version and compares
against the manifest's stored `IdentitySha256`.

A partial binary — real bytes only at proven sites, zeroed/padded elsewhere —
would present a `copy` whose non-site bytes differ from the real OAE 1.5.2
file's non-site bytes at essentially every position, so its SHA-256 would
almost certainly not equal the recorded `identitySha256`. It would fail to
identify as OAE 1.5.2 exactly like `Fixtures.UnrecognisedBuild()`
(`tests\patcher_gui\Fixtures.cs:125-132`) does deliberately for a single
flipped byte. There is no partial-file shortcut: exercising the real hash
requires the complete, byte-exact real file.

**Conclusion: confirmed** — a partial/redacted binary cannot reproduce or
meaningfully exercise the real allowlist hash.

## 3. Worth doing for v1.1?

No. Combining 1 and 2: the only binary that could exercise the real hash
(the full file) can't legally be held by the repo, and no legally-safe
substitute (partial file) is technically capable of exercising that hash
logic at all — hashing the whole file is the entire point of the design (see
`Manifest.cs:151-154` doc comment: build identity must catch *any* byte
drift, not just site drift). There's no middle path to build here.

This doesn't block shipping: the beta already validated the real OAE 1.5.2
build by hand once (see ticket 07's background), and the synthetic fixture in
`Fixtures.cs` already exercises the allowlist *mechanism* honestly (its own
hash is computed and admitted the same way `IdentityOf` computes and compares
real hashes — see `Fixtures.cs:9-10` and `Admit()` at line 188-194). What's
untested in CI is only whether the *specific recorded* OAE 1.5.2 hash still
matches a real file, which by design requires a real file. Recommend closing
this ticket as a documented, accepted gap rather than pursuing an
implementation.
