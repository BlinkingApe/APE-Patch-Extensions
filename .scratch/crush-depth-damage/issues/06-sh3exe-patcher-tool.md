# sh3.exe patcher tool

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

A standalone, safely re-runnable tool that inserts the `ape.act` `LoadLibrary` call site into a given `sh3.exe`, automating the manual hand-patch proved out in 02-re-sh3exe-load-call-site. Used both internally (reapplying the call site after OAE reapplies its patch suite) and eventually by end users (per ADR-0005), so it must never redistribute modified copyrighted binary bytes — it patches the user's own local copy.

## Acceptance criteria

- [ ] Given a real, unpatched OAE sh3.exe, the tool inserts the confirmed `LoadLibrary("ape.act")` call site from 02-re-sh3exe-load-call-site.
- [ ] Running the tool against a patched sh3.exe again does not corrupt it (safe to re-run, e.g. detects the patch is already present and no-ops, or is otherwise idempotent-safe).
- [ ] End-to-end verification: patch a real OAE sh3.exe, launch the game, and confirm `ape.act` (from 05-ape-act-hook-install-and-wiring) loads and its debug log shows the damage loop running.
- [ ] The tool operates only on the user's local file — it does not require or produce a redistributable copy of any Ubisoft/OAE-derived binary.

## Blocked by

- 02-re-sh3exe-load-call-site (needs the confirmed call site)
- 05-ape-act-hook-install-and-wiring (needs a real ape.act to prove the patch actually loads it and the damage loop runs)
