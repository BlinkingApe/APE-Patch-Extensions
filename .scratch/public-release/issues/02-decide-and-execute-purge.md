# Decide and execute purge of audit findings

Type: grilling
Status: resolved
Blocked by: 01

## Question

Given the findings list from [Audit repo for copyrighted game data, secrets, and personal info](01-audit-game-data-secrets-pii.md), decide keep/purge/redact per finding, and execute — including a git history rewrite (e.g. `git filter-repo`) if anything needs removing from history and not just from HEAD.

## Answer

Decided and executed per finding:

- **`tests/APE Testing Missions/`** (untracked stock SH3 missions) — **delete**. Never made it into history, so no rewrite needed; the folder is removed from the working tree.
- **`tests/patcher_gui/fixtures/hsie.ini`** (verbatim OAE 1.5.2 hsie.ini, committed in `9165c5b`) — **purge from history and replace with a minimal synthetic fixture**. Ran `git filter-repo --invert-paths --path tests/patcher_gui/fixtures/hsie.ini` (repo has no remote, so this was a local-only, low-blast-radius rewrite — no force-push or collaborator coordination needed). The file is gone from every commit. Rebuilt `tests/patcher_gui/fixtures/hsie.ini` from scratch: it keeps the structural quirks the test suite actually asserts on (CRLF endings, key order, comment lines flanking the internal-values block, trailing tabs on Val13/14/15) and the eight `Val9`–`Val16` lines match `data/manifest/ape-manifest.json`'s `"original"` text exactly, but every surrounding key (Val1–8, Val17–24, nParam, Adr9, Typ9) and comment is placeholder prose, not h.sie's real documentation or OAE's real tuning. Updated the stale "hsie.ini is the real thing" comments in `Fixtures.cs` and `IniFileTests.cs` to describe the new synthetic fixture. Full suite (`tests/patcher_gui/run.cmd`) passes: 140/140 across every culture the runner sweeps.
- **`tests/patcher_gui/fixtures/en_menu.txt`** (3 lines of verbatim stock menu strings, committed in `4277506`) — **keep as-is**. Low-risk enough (three short UI strings) not to warrant a history rewrite.
- **Commit author email** (`blinkingape@gmail.com`, present in every commit) — **keep as-is**. It already matches the user's existing public modding handle, so it's an intentional public alias rather than a leak.

No git history rewrite was needed for the missions folder, the menu-text fixture, or the author email — only `hsie.ini` required one, and it's done.
