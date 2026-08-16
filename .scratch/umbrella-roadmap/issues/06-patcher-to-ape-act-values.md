# How patcher-set values reach ape.act's runtime logic

Type: grilling
Status: resolved

## Blocked by

05

## Question

Surfaced 2026-08-15 by the hull-repair scope resolution
(`03-hull-repair-patch-scope.md`). Dropping `ape.cfg` removes the runtime
*config file*, but **not** `ape.act`'s runtime logic: crush-depth damage is
computed live (HP-per-game-second bands while below crush depth) and cannot be
expressed as a static byte patch to `sh3.exe`. So APE-owned constants still have
to reach a running `ape.act` somehow, with no config file to read.

✅ **Premise confirmed 2026-08-15** while resolving `05-gui-patcher-scope.md`.
The user was put the choice directly and chose two write targets: dropping
`ape.cfg` drops the config *file*, **not** `ape.act`'s runtime logic. Crush-depth
damage stays live-computed. **This ticket is not void** and the crush-depth model
does not need re-opening. The ⚠ that stood here is discharged.

Also settled by 05, and constraining the answer below:

- The patcher is **C# WinForms on .NET Framework 4.x**, writing into a PE32 DLL
  that APE itself builds — so APE controls `ape.act`'s layout completely, and the
  patcher needs no PE parsing to find a marked struct.
- `ape.act` lives in the **game root folder**; APE is not a JSGME mod, so there is
  no MODS-source-vs-installed-copy question to answer.
- Patch definitions live in a **generated manifest embedded in the patcher**
  (see 08), so wherever the constants land, the locating rule is manifest data —
  not hardcoded offsets in C#.

Decide:

- **Mechanism.** Does the patcher write constants into `ape.act`'s own binary
  (a known data section it patches like any other target), or does `ape.act`
  read them back out of the patched `sh3.exe`, or something else?
- **If patching `ape.act`:** where do the constants live so they are findable and
  verifiable — a dedicated aligned struct with a magic marker, so the patcher can
  locate it without hardcoded offsets and refuse to write on mismatch?
- **Defaults and failure mode.** ADR-0003 and the existing loop design commit APE
  to *failing inert*. What does `ape.act` do if its constants were never patched,
  or the marker is missing — baked-in defaults, or refuse to run?
- **What retiring `src/core/config.{hpp,cpp}` costs.** It is shipped, tested code
  with a `config_check` target. Is it deleted, or repurposed as the struct the
  patcher writes into?
  **Narrowed by ticket 02 (resolved 2026-08-15):** expect **deletion, not
  repurposing**. 02 decided the binary is the sole store and the patcher **reads
  values back out of `ape.act`**, so what is needed is a magic-markered,
  fixed-layout struct the patcher can locate and both read and write — not a
  config *reader*. That is a different artifact from `config.{hpp,cpp}`.
- **Read-back applies here too.** 02's rule is not `sh3.exe`-only: the patcher must
  be able to read APE's current constants out of `ape.act` to populate the GUI,
  with no sidecar. Whatever mechanism this ticket picks has to support **read**,
  not just write.
- **ADR.** This reverses ADR-0004 (config format and location). Write the
  superseding ADR as part of resolving this.

## Notes

Use `/domain-modeling` — this changes what "config" means across the whole
project, and the term is currently defined around a runtime file.

## Answer

Resolved 2026-08-16 via `/grilling`. Superseding ADR:
`docs/adr/0006-ape-act-owned-constants-live-in-a-marked-struct-the-patcher-deploys.md`.

**Mechanism.** Constants live in a magic-marked struct baked into `ape.act`'s own
`.data` — not read from `sh3.exe`. But the framing that surfaces this decision
changed along the way: `ape.act` is no longer assumed to already sit in the game
root waiting to be edited. It becomes something **the patcher deploys**. APE's
C++ build still produces `ape.act`, but as a *template* embedded as a resource
inside the C# patcher exe. The patcher copies that template into the game root
and patches it **only when at least one runtime feature is enabled** — no
runtime feature enabled means no DLL installed at all. Unchecking the last such
feature **deletes `ape.act`** from the game root, mirroring ticket 02's "revert
is just unchecking."

**Locating the struct.** Magic-marker struct, offset recorded in the manifest at
APE's own build time (it already knows the offset — it just linked the DLL).
The patcher seeks to that offset in its embedded template copy and verifies the
marker before writing, same context/field verify-before-write shape as every
`sh3.exe` site. No runtime AOB scanning — consistent with ticket 05.

**Struct scope.** One struct per mod, each separately magic-marked. Not a
shared, versioned mega-struct — a future mod needing live constants just adds
its own struct alongside crush-depth damage's, and the manifest gains another
entry.

**Read-back.** When a deployed `ape.act` exists in the game root, the GUI reads
current values from it. When it doesn't (nothing enabled yet, or just
reverted), the GUI reads the same struct out of the patcher's own embedded
template instead — same offset, different source file.

**Defaults and failure mode.** `ape.act` verifies its own marker at startup and
**fails inert on mismatch** — consistent with ADR-0003 and the existing
`UnresolvedGameState` fallback in `dllmain.cpp`. A struct that doesn't check out
means the loop for that mod doesn't run, the same way a missing `ape.cfg`
currently makes it log and return.

**What retiring `config.{hpp,cpp}` costs.** Confirmed deletion, not
repurposing: `src/mods/crush_depth_damage/config.{hpp,cpp}` (the file ticket 02
called `src/core/config.{hpp,cpp}` before the reorg settled its real location)
and `tools/config_check` are dead once this is implemented. A magic-markered,
read-and-write struct is a different artifact from an INI reader.

**Left open, not blocking this ticket.** Whether `sh3.exe`'s dedicated
`LoadLibrary` call site for `ape.act` (ADR-0002) tolerates `ape.act` being
absent has not been RE'd — h.sie's/NSAP's own call sites are fire-and-forget,
which is why ADR-0002 mirrors that shape, but nobody has disassembled *this*
call site to confirm it discards the result the same way. Flagged for whichever
ticket implements the actual `sh3.exe` patch for that call site.
