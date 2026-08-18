# Version numbering and display scheme

Type: grilling
Status: resolved

Map: [v1.1 scope map](../map.md)

## Blocked by

None

## Question

What version number does an APE build carry, in what format, and where is it
surfaced? Candidates raised in [ticket 02](02-v1.1-polish.md): the exe's file
name (`dist/ApePatchExtensions.exe`, hardcoded in `src/patcher_gui/build.cmd`)
and the window header (`MainForm.cs`'s `RenderHeader()`, which already shows
`InstallPath` / `BuildName` / `HsieVersion` / `Sha256` but nothing about APE's
own version).

Open sub-questions:

- Format — semver (`1.1.0`)? A bare `1.1`? Does it need a build/commit suffix
  for dev builds between releases?
- Where does the version number live as a source of truth — a constant in
  code, a file `build.cmd` reads, an assembly attribute?
- Surfaced where — exe filename, window header, both? If the filename, does
  the exe get renamed per release (`ApePatchExtensions-1.1.exe`) or does the
  version live inside a fixed-name exe only?
- Does `build.cmd` need to fail if the version isn't bumped, the way ticket
  08's manifest-drift check does — or is that over-engineering for a two-item
  polish release?

## Answer

Resolved via `/grilling` 2026-08-18:

1. **Format**: bare two-part `1.1` — no full semver, no dev/commit suffix.
   The Sha256 already shown in the header covers "which exact build";
   the version number only needs to mean "which release."
2. **Source of truth**: a single `const string ApeVersion = "1.1";` in a
   new `src/patcher_gui/Version.cs`, read by both `MainForm.cs` (header)
   and `build.cmd` (filename). No `AssemblyInfo.cs` (none exists in this
   codebase today — raw `csc` off `*.cs` globs, no project file) and no
   separate JSON/text file.
3. **Surfaced**: both the window header (next to `BuildName` /
   `HsieVersion` / `Sha256`) and the exe filename, which is no longer
   fixed — `dist/ApePatchExtensions-1.1.exe` per release.
4. **No version-bump build guard.** Unlike ticket 08's manifest-drift
   check (which prevents a silent correctness bug — wrong offsets baked
   into a shipped exe), a stale version number is low-stakes and has no
   reliable automated staleness signal in a repo with no version tags.
   The Sha256 remains the real ground truth if it ever goes stale.
