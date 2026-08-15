# How patcher-set values reach ape.act's runtime logic

Type: grilling
Status: open

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
- **ADR.** This reverses ADR-0004 (config format and location). Write the
  superseding ADR as part of resolving this.

## Notes

Use `/domain-modeling` — this changes what "config" means across the whole
project, and the term is currently defined around a runtime file.
