# Settings model for the GUI patcher (supersedes: shared ape.cfg strategy)

Type: grilling
Status: open

## Blocked by

05

## Question

**Superseded 2026-08-15** by the hull-repair scope resolution
(`03-hull-repair-patch-scope.md`). This ticket originally asked how subsystems
share one `ape.cfg`. That question is moot: `ape.cfg` is being **dropped**, and
every APE setting becomes a static byte written by a shipped GUI patcher.

The successor question — settings still need a model, just a different one:

- **What is a "setting" now?** Three kinds have already surfaced and may not want
  the same treatment: (a) an h.sie flag-table byte at `0x63CF00` — pure on/off;
  (b) an operand or constant inside a patch cave, e.g. hull repair's roll floor
  and width; (c) an APE-owned runtime constant that `ape.act` computes with, e.g.
  crush-depth band rates. Does the patcher present these uniformly, or does the
  UI distinguish them?
- **Where does the user's chosen value persist between patcher runs?** The
  patched `sh3.exe` is the source of truth for what is *currently applied*, but
  reading values back out of patched bytes to repopulate the GUI is awkward.
  Does the patcher keep its own sidecar state file — and if so, has `ape.cfg`
  been reinvented under another name?
- **Re-patching and idempotency.** OAE and h.sie already byte-patch `sh3.exe`.
  Applying APE's patcher twice, or over an OAE update, must not corrupt or
  double-apply. Verify-expected-old-byte (patch1337 style) or full
  detect-and-rewrite?
- **Uninstall/revert.** JSGME expects a clean disable. Does the patcher keep a
  backup of the original bytes, and where?

Answer should be concrete enough that the patcher's UI and file layout can be
designed without re-opening it.

## Notes

Depends on the patcher's overall scope (05) — take that first.
The ADR-0004 reversal this implies is tracked in the map's Decisions-so-far.
