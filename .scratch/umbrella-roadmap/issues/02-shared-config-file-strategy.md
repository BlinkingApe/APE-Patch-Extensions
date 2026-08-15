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
- **Uninstall/revert.** ⚠ **This bullet's premise is dead.** It was written as
  "JSGME expects a clean disable" — but 05 decided APE has **zero JSGME
  interaction ever**; both `sh3.exe` and `ape.act` live in the game root and are
  patched in place. Revert is now entirely APE's own problem, with no mod-enabler
  to lean on. Re-ask it that way: does the patcher keep a backup of the original
  bytes, where, and what does "uninstall APE" even mean now?

Answer should be concrete enough that the patcher's UI and file layout can be
designed without re-opening it.

## Notes

~~Depends on the patcher's overall scope (05) — take that first.~~
**05 is resolved (2026-08-15).** What it settled that bears on this ticket:

- Only **two** of the three "kinds of setting" listed above are in scope. Kind
  (a), the h.sie flag-table byte, is **out** — APE never presents or writes the
  flag table; `HsieOptionsSelector` keeps it. Only (b) cave operands and (c)
  APE-owned `ape.act` constants remain, so the "does the UI distinguish them?"
  question is now a two-way, not a three-way.
- **Idempotency has a decided mechanism**: AOB signature match plus
  verify-expected-old-bytes per site, refusing that site on mismatch. What is
  *not* decided is how a value already written is read back — verifying an old
  byte works for a known default but not for a user's previously-chosen value,
  which is exactly the "sidecar state file / has ape.cfg been reinvented?"
  question below. **That is the live half of this ticket.**
- Re-patching over `HsieOptionsSelector` is a **non-issue**: it writes flag bytes
  only, which APE never touches. Disjoint byte ranges, no conflict logic needed.

The ADR-0004 reversal this implies is tracked in the map's Decisions-so-far.
