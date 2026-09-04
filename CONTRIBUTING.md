# Contributing to APE

PRs are welcome — bug fixes, new tunable patches, build/tooling improvements.
There's no formal process; open a PR or an issue and it'll get reviewed.

## Before you dig in

This is a reverse-engineering-heavy project: most features are patches
derived from disassembling a specific `sh3.exe` build, not original game
logic. Some context worth reading first:

- [`CONTEXT.md`](CONTEXT.md) — project glossary and domain vocabulary (what
  "sh3.exe", "AOB pattern scan", and other repo-specific terms mean).
- [`docs/adr/`](docs/adr) — architecture decisions and why past alternatives
  were rejected.
- [`docs/re-notes/`](docs/re-notes) — reverse-engineering write-ups for each
  patch's signature/offset, for anyone extending or re-verifying a patch.
- [`.scratch/`](.scratch) — this project's issue tracker. Feature work is
  planned as markdown tickets under `.scratch/<feature>/`, not GitHub Issues
  alone; see [`docs/agents/issue-tracker.md`](docs/agents/issue-tracker.md)
  for the convention if you want to work the same way.

## Build and test

See the root [README](README.md#building-from-source) for toolchain setup.
Run the relevant test suite before opening a PR:

```
tests\patcher_gui\run.cmd            GUI patcher tests
ctest --preset x86-debug             ape.act runtime tests
```

**Culture invariance is a correctness requirement for the GUI patcher, not a
style preference.** `hsie.ini` is parsed by h.sie's own code, which expects
`4.1667e-08` — a comma-decimal locale silently renders any written value
inert. `tests\patcher_gui\run.cmd` runs the full suite under both the
invariant culture and `de-DE`; a PR that only passes under one is not done.

## Adding a new patch

A new patch's offsets and byte layout should be independently verified
against a real `sh3.exe`/`hsie.ini` before being added to the manifest
(`tools/re-static/gen_manifest.py`) — see `docs/re-notes/` for the pattern
existing patches follow. `build.cmd` regenerates the manifest and fails the
build on drift, so keep the generator and the checked-in manifest in sync.

## Scope

APE targets `sh3.exe` and reads (never writes)
h.sie's own flag table — `HsieOptionsSelector` stays the tool for h.sie's
existing toggles. PRs that widen APE into re-exposing h.sie's flags or
supporting unrelated SH3 mod stacks are likely out of scope; open an issue
first if you're unsure before investing time in one.
