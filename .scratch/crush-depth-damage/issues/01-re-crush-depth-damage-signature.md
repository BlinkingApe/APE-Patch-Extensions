# RE: locate crush-depth damage function

Status: ready-for-human

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

A documented, versioned AOB signature identifying the crush-depth damage calculation function in the real OAE sh3.exe, produced via Cheat Engine (dynamic discovery) and x32dbg (disassembly confirmation), per ADR-0003.

## Acceptance criteria

- [ ] The target function performing crush-depth hull damage is located in the currently-installed OAE sh3.exe.
- [ ] An AOB (array-of-bytes) signature with appropriate wildcards is derived and confirmed to uniquely match that function's entry point.
- [ ] The signature, the OAE build/version it was verified against, and any notes on volatile bytes are recorded in `docs/re-notes/`.

## Blocked by

None — can start immediately.
