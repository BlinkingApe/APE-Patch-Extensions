# Audit repo for copyrighted game data, secrets, and personal info

Type: task
Status: resolved

## Question

Sweep the full git history and the current working tree for:

- Copyrighted/third-party game assets — stock or OAE SH3 data files, missions, textures, executables (like the `tests/APE Testing Missions/` folder just left untracked on `wayfinder/04-diesel-damage-reopen`).
- Secrets/credentials of any kind.
- Personal-identifying info — local file paths (e.g. `d:\code\...`), usernames, machine-specific config.

Produce a findings list: for each hit, the file path(s), which commit(s) introduced or touched it, and which of the three categories it falls under. Do not decide remediation here — that is [Decide and execute purge of audit findings](02-decide-and-execute-purge.md).

## Answer

Method: enumerated every file ever added across all branches (`git log --all --name-only --diff-filter=A`, 190 paths), scanned full history diffs for secret/credential and PII patterns, listed all blobs >20KB across history to catch unexpected binary game assets, and inspected the untracked working-tree folder called out in the map's Notes.

### Findings

**1. Copyrighted third-party game assets**

- `tests/APE Testing Missions/` — **untracked working-tree folder, never committed** (absent from the full add-list). Contains genuine stock SH3 Naval Academy training missions (Convoy Attack, Flak Artillery, Naval Artillery, Navigation, Torpedoes — `.mis`/`Media.cfg`/manual `.log` files in English/French/German, 39 files, ~213 KB). It is **not** covered by `.gitignore`, so it is one `git add -A` away from landing in history. Clear-cut game data; not currently a history problem, but a live footgun.
- `tests/patcher_gui/fixtures/hsie.ini` (introduced in commit `9165c5b`, "GUI patcher: the real tool, folded out of prototype variant C") — a full, verbatim copy of h.sie's hardcode-patch INI file: all of h.sie's original comments, parameter documentation, and internal `AdrN`/`TypN` tables, not just the key=value shape needed for the test. Third-party mod-author content, committed as-is.
- `tests/patcher_gui/fixtures/en_menu.txt` (introduced in commit `4277506`, "Diesel damage report: reword the message and fire it unconditionally") — 3 lines of verbatim stock-game menu strings copied from SH3's `en_menu.txt` ("Diesel engine damaged!", etc.). Small, but literal copied game text.
- `data/manifest/ape-manifest.json` / `tools/re-static/gen_manifest.py` — reverse-engineered byte offsets and replacement bytes derived from h.sie/OAE hardcode patches. This is APE's core content (not incidental), and its legal/attribution status is explicitly the subject of [Settle attribution stance for reverse-engineered patch data](03-re-patch-data-attribution-stance.md) — flagged here for completeness, not re-litigated; no separate action needed from this ticket.

**2. Secrets/credentials**

- None found. Full-history grep for API-key/token/password/private-key patterns turned up only false positives (code comments using the word "token" in the tokenizer sense, and a `publicKeyToken=` attribute in `app.manifest`, which is a .NET strong-name identifier, not a secret).

**3. Personal-identifying info**

- No local file paths (`C:\Users\...`, `D:\code\...`) or machine-specific strings found anywhere in tracked history.
- Every commit's author identity is `blinkingape <blinkingape@gmail.com>`. This is a real email address that will become publicly visible in the git history once the repo goes public. It matches the user's existing public modding handle (`data/art/BlinkingApe (OA modder).tga`, current git user `blinkingape`), so it is likely an intentional, already-public alias rather than a leak — but it's called out here since it's the one real personal-data item the audit surfaced, so [Decide and execute purge of audit findings](02-decide-and-execute-purge.md) can make an explicit keep/change call rather than it going unnoticed.
- `third_party/minhook/AUTHORS.txt` lists third-party maintainers' own emails — expected, unmodified content of the vendored MIT-licensed library, not a repo-introduced PII issue.

**No other findings.** A full-history large-blob scan (every blob >20 KB ever committed) turned up nothing beyond the known art assets (`data/art/*`, the user's own logo, non-copyrighted), the vendored `doctest.h`, and ordinary source/doc files — no stray `.dat`/`.dds`/`.wav`/`.tga`/mission files snuck into history anywhere.
