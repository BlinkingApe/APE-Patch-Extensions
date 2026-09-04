# Draft public-facing docs

Type: grilling
Status: resolved
Blocked by: 03

## Question

Decide and draft the first-contact public docs: README.md (what APE is, install/build instructions, an attribution section reflecting [Settle attribution stance for reverse-engineered patch data](03-re-patch-data-attribution-stance.md)'s answer), a LICENSE file (MIT text), and CONTRIBUTING.md (how outside contributors engage).

## Answer

Two judgment calls settled with the user, then drafted:

- **Contribution policy: PRs welcome, no restrictions.** No formal process —
  `CONTRIBUTING.md` points contributors at `CONTEXT.md`, `docs/adr/`,
  `docs/re-notes/`, and the `.scratch/` issue tracker for context, states the
  build/test steps (including the culture-invariance test requirement — see
  [`src/patcher_gui/README.md`](../../src/patcher_gui/README.md)), and flags
  one scope boundary carried over from the umbrella map: PRs that re-expose
  h.sie's own flag table are out of scope (`HsieOptionsSelector` owns those).
- **Install instructions: pre-built binaries, published immediately.** The
  user will publish `ApePatchExtensions.exe` via GitHub Releases at launch,
  so README leads with a Releases download link (relative `../../releases`,
  works once pushed) rather than build-from-source-only. Build instructions
  are still included for developers, sourced from `src/patcher_gui/README.md`
  and the root `CMakePresets.json`.

Three files created at repo root:

- **`README.md`** — what APE is (GUI patcher + `ape.act` runtime, target
  build OAE 1.5.2/`v 1.6E1`), Releases download, build-from-source steps,
  project layout, an attribution section per ticket 03's answer (credits
  h.sie and Stiebler, names `sh3.exe` as the target binary, no hedge
  language), and a license pointer.
- **`LICENSE`** — standard MIT text, copyright `blinkingape` 2026, per this
  map's Notes (APE's own code is MIT; vendored `third_party/` libs keep their
  own already-compatible MIT licenses, noted in the README rather than
  duplicated here).
- **`CONTRIBUTING.md`** — as described above.
