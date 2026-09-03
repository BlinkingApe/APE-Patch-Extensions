# Audit repo for copyrighted game data, secrets, and personal info

Type: task
Status: open

## Question

Sweep the full git history and the current working tree for:

- Copyrighted/third-party game assets — stock or OAE SH3 data files, missions, textures, executables (like the `tests/APE Testing Missions/` folder just left untracked on `wayfinder/04-diesel-damage-reopen`).
- Secrets/credentials of any kind.
- Personal-identifying info — local file paths (e.g. `d:\code\...`), usernames, machine-specific config.

Produce a findings list: for each hit, the file path(s), which commit(s) introduced or touched it, and which of the three categories it falls under. Do not decide remediation here — that is [Decide and execute purge of audit findings](02-decide-and-execute-purge.md).
