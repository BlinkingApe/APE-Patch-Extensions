# Settings model for the GUI patcher (supersedes: shared ape.cfg strategy)

Type: grilling
Status: resolved

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

## Answer

Resolved 2026-08-15, same day as 05, which had already settled or killed three of
the four bullets. **`ape.cfg` is genuinely gone — not renamed.** The patched
binaries are the only store APE has.

### 1. What a "setting" is now — two kinds, but the UI doesn't organise by them

Kind (a), the h.sie flag-table byte, is **out of scope** (ticket 05): APE reads
those flags but never writes them. Two kinds remain:

- **(b) a cave operand or constant in `sh3.exe`** — version-keyed offset, and
  gated on its h.sie patch being enabled;
- **(c) an APE-owned constant in `ape.act`** — no version key, no h.sie
  dependency, located via ticket 06's mechanism.

**The UI groups by feature, not by write target.** A user thinks "hull repair",
"crush-depth damage" — not "things in `sh3.exe`" vs "things in `ape.act`". Which
file a value lands in is an implementation detail the manifest knows and the GUI
never surfaces.

### 2. Persistence — the binary is the single source of truth

The patcher **reads current values straight out of `sh3.exe` and `ape.act`** to
populate the GUI. There is **no sidecar state file of any kind**.

This is h.sie's own model, and the data proves he uses it: `HSIEContents.txt`
declares the slider array's offset, and the arrays genuinely differ per install
(OAE 1.5.2 `[3.5, 1.0, 0.25, …]` vs KC2.5 `[2.0, 2.5, 0.25, …]`). User choices
live in the exe and are read back.

**How verification and mutability coexist** — each manifest site is split in two:

- **invariant context bytes** — never change, verified before any write; these
  are what prove the offset is really the site we think it is;
- **the mutable field** — read back to populate the GUI, overwritten on save.

Verify-before-write therefore checks the *context*, not the field. This is what
makes read-back safe, and it is why no sidecar is needed to remember intent.

Consequence: nothing can desync. Replace `sh3.exe` and the GUI simply shows what
that binary actually contains.

### 3. Re-patching and idempotency — settled by construction

Re-running is safe because fields are read back rather than blindly re-applied,
and each write is gated on its context bytes matching. Applying twice is a no-op
on unchanged values.

**Running over `HsieOptionsSelector` is a non-issue**: it writes flag bytes only
(readme confirmed), and APE never writes those. Strictly disjoint byte ranges,
so no conflict detection, no ordering requirement, no co-existence logic.

**Running over an OAE update** is caught by the version allowlist plus the
context-byte check — a moved or rewritten site refuses and reports rather than
corrupting.

### 4. Revert — there is no "uninstall", only unchecking

**A user undoes an APE edit the same way they undo a h.sie edit: run the patcher
again and disable the patch.** Unchecking a feature writes its **original bytes**
back. That is the entire revert mechanism — no separate uninstall flow, no
"restore defaults" command, no whole-file backup and restore.

Two properties this buys:

- **Surgical.** Only APE's own bytes are touched, so any `HsieOptionsSelector`
  toggles the user set *after* APE patched are left alone. A whole-file restore
  would have silently reverted those.
- **Nothing goes stale.** There is no backup file to drift out of date when OAE
  updates the exe.

**Requirement this places on the manifest (ticket 08):** every site must carry its
complete **original byte run**, not merely a few context bytes for verification —
unchecking has to reconstruct the pre-APE state exactly.

**⚠ "APE off" ≠ "feature off".** These are two distinct, both-reachable states:

| | Result |
|---|---|
| Hull repair **unchecked** | Original bytes restored — h.sie's stock roll `rand(0..15)+5` runs again |
| Hull repair **checked, range `0..0`** | APE applied, hull repair yields nothing |

Ticket 03's "off is the bottom of the range, not a separate mechanism" decision
survives intact — it describes the *second* row. The checkbox is a different axis.
The UI must not blur them.

**No automatic file backup** (judgment call, not asked): the patcher's own revert
is complete for APE's edits, so a backup would only insure against damage APE
didn't cause. APE's docs should still advise keeping a manual `sh3.exe` backup,
matching what Stiebler's readme "very strongly" advises — say it, don't automate
it. Flag this if you'd rather the tool took one.

### Knock-on effects

- **ADR-0004 (config format and location) is now fully superseded**, not merely
  reversed: there is no config file, and the store is the patched binary. The
  superseding ADR can be written from this ticket without re-opening anything.
- **`src/core/config.{hpp,cpp}` and the `config_check` target are dead.** Ticket
  06 asked whether they might be repurposed as the struct the patcher writes into
  — read-back means that struct needs a magic marker and a fixed layout, which is
  not what a config *reader* is. Expect deletion, not repurposing.
- **Ticket 08 gains a requirement**: manifest sites carry original byte runs plus
  a context/field split.
