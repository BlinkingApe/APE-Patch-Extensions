# Public release cleanup

Label: wayfinder:map

## Destination

The repo, including its full git history, is verified free of copyrighted third-party game assets, secrets, and personal info; carries an MIT license and first-contact public docs (README, LICENSE, CONTRIBUTING); and has a settled, documented answer on the legal/attribution status of its reverse-engineered h.sie/OAE/Stiebler patch data. Ready for the user to flip GitHub visibility to public themselves — that flip is not part of this map.

## Notes

- Target platform: public GitHub repo.
- License: MIT for APE's own code (already decided). Vendored `third_party/` libs (doctest, minhook) keep their own licenses — both already MIT, compatible.
- "Clean" is a full-history standard, not just current-tree: if the audit finds a violation in an old commit, history gets rewritten/purged, not just deleted going forward.
- `.scratch/` issue-tracker content (including this map) is normal public-repo content — it gets the same secrets/PII/copyright sweep as everything else, no separate scrubbing pass.
- Consult `/grilling` for the judgment-call tickets below.

## Decisions so far

- [Audit repo for copyrighted game data, secrets, and personal info](issues/01-audit-game-data-secrets-pii.md) — 4 findings: untracked `tests/APE Testing Missions/` (stock game missions, not yet committed, not gitignored), verbatim `hsie.ini`/`en_menu.txt` test fixtures, the already-tracked manifest attribution question, and the public commit-author email. No secrets, no leaked local paths, no stray game binaries in history.

## Not yet specified

- Whether to set up public-facing CI (build/test workflow) before or after going public — not required for "ready," worth deciding later.
- GitHub repo-level presentation (description, topics, social preview image) — cosmetic, not blocking.
- Exact README wording for crediting OAE/h.sie/Stiebler/NSAP depends on [Settle attribution stance for reverse-engineered patch data](issues/03-re-patch-data-attribution-stance.md)'s outcome — not specifiable until that resolves.

## Out of scope

(none yet)
