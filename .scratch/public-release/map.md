# Public release cleanup

Label: wayfinder:map

## Destination

The repo, including its full git history, is verified free of copyrighted third-party game assets, secrets, and personal info; carries an MIT license and first-contact public docs (README, LICENSE, CONTRIBUTING); and has a settled, documented answer on the legal/attribution status of its reverse-engineered h.sie/OAE/Stiebler patch data. Ready for the user to flip GitHub visibility to public themselves — that flip is not part of this map.

**Status 2026-09-04 — reached. No open tickets remain.** All four tickets are resolved: the audit found and the purge fixed the only real findings, attribution is settled, and first-contact docs are drafted. The two items under **Not yet specified** are both explicitly non-blocking (CI, cosmetic repo presentation) and were never tickets. Ready for the user to flip visibility.

## Notes

- Target platform: public GitHub repo.
- License: MIT for APE's own code (already decided). Vendored `third_party/` libs (doctest, minhook) keep their own licenses — both already MIT, compatible.
- "Clean" is a full-history standard, not just current-tree: if the audit finds a violation in an old commit, history gets rewritten/purged, not just deleted going forward.
- `.scratch/` issue-tracker content (including this map) is normal public-repo content — it gets the same secrets/PII/copyright sweep as everything else, no separate scrubbing pass.
- Consult `/grilling` for the judgment-call tickets below.

## Decisions so far

- [Audit repo for copyrighted game data, secrets, and personal info](issues/01-audit-game-data-secrets-pii.md) — 4 findings: untracked `tests/APE Testing Missions/` (stock game missions, not yet committed, not gitignored), verbatim `hsie.ini`/`en_menu.txt` test fixtures, the already-tracked manifest attribution question, and the public commit-author email. No secrets, no leaked local paths, no stray game binaries in history.
- [Decide and execute purge of audit findings](issues/02-decide-and-execute-purge.md) — Deleted the untracked missions folder; purged `hsie.ini` from git history (`git filter-repo`, no remote so no force-push needed) and replaced it with a synthetic fixture that keeps only the structural quirks the tests need; kept `en_menu.txt` and the author email as-is (low risk / already-public alias).
- [Settle attribution stance for reverse-engineered patch data](issues/03-re-patch-data-attribution-stance.md) — Low risk, treated as functional/interoperability data (like a binary-diff format), no legal-hedge language; README credits h.sie and Stiebler (the actual patch-logic sources) and names `sh3.exe` as the target binary — not OAE, GWX/KC, or NSAP.
- [Draft public-facing docs](issues/04-public-docs.md) — Created `README.md` (leads with a GitHub Releases download, since pre-built binaries ship at launch; build-from-source steps; attribution per ticket 03), `LICENSE` (MIT, `blinkingape` 2026), and `CONTRIBUTING.md` (PRs welcome, no restrictions; points at `CONTEXT.md`/`docs/adr/`/`.scratch/` for context).

## Not yet specified

- Whether to set up public-facing CI (build/test workflow) before or after going public — not required for "ready," worth deciding later.
- GitHub repo-level presentation (description, topics, social preview image) — cosmetic, not blocking.

## Out of scope

(none yet)
