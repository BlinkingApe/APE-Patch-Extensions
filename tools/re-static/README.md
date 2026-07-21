# Static RE helper scripts (sh3.exe)

Scripted static-analysis pass for issue 01's value anchors. Binary Ninja is
GUI/personal on this machine (no headless API), so these use Python `pefile` +
`capstone` for the reproducible static half. See
`docs/re-notes/01-crush-depth-damage-signature.md` §5 for context.

## Setup

```
python -m pip install pefile capstone
```

Point the `EXE = ...` path at the target `sh3.exe`, then run each script.
Verify the binary first: it must be OAE build `4B6BEE20` (see
`docs/re-notes/oae-builds.md`) — a different build means the addresses below
may have moved, which is exactly what the AOB signatures are meant to catch.

## Scripts

- `sh3re.py` — AOB signature extraction + uniqueness count over `.text`.
- `chains.py` — real memory-operand xrefs (filtered to actual `[abs]` operands,
  not coincidental immediates), function-start finder, and E8 caller finder.
- `probe.py` — tally of offsets dereferenced off a candidate singleton chain
  (`[[0x5449B0]+0xD8]`), used to rule it in/out as a player-object root.

These cover only the **static** half (code anchors, AOB signatures). Rooting the
live player pointer chains, CDT, and the mission clock is dynamic work (Cheat
Engine on the running game) — see the RE note §4.
