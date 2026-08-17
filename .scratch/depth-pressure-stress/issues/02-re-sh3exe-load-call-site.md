# RE: locate/construct the sh3.exe load call site for ape.act

Status: ready-for-human

## Parent

.scratch/depth-pressure-stress/PRD.md

## What to build

A confirmed location and patch shape within OAE sh3.exe for inserting a dedicated `LoadLibrary("ape.act")` call, per ADR-0002 — proved by hand-patching a test copy of sh3.exe and observing a placeholder DLL actually load.

## Acceptance criteria

- [ ] A suitable call site (location, surrounding bytes, and any code-cave needs) is identified in OAE sh3.exe for inserting a new `LoadLibrary` call, independent of the existing `hsie.act`/`sh5.act` call sites.
- [ ] A test copy of OAE sh3.exe is hand-patched at this site to load a placeholder DLL named `ape.act` (e.g. one that only writes a log line on `DllMain`), and the placeholder is confirmed to load when the game starts.
- [ ] The call site details (offset/location, patch bytes, and the placeholder-load proof) are recorded in `docs/re-notes/`, versioned against the OAE build it was verified on.

## Blocked by

None — can start immediately.
