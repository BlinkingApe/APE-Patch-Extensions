# ape.act-owned constants live in a marked struct inside a patcher-deployed ape.act, not a config file

> **Status note (2026-11-25): the `ape.act` half is implemented, for depth stress**
> (`.scratch/depth-pressure-stress/`, ticket 21). Crush-depth damage was dropped (ADR-0008);
> depth stress is the first mod with a struct. `ape.act` carries it in `.data`
> (`src/mods/depth_stress/constants.hpp`, marker `APEDPTH1`, version 1, 48 bytes of float32
> fields in runtime units), verifies it at startup and fails inert on a mismatch. A Release
> build records the struct's file offset and field layout under the manifest's `apeAct` key.
> The patcher half (embed, deploy, read-back, delete on revert) is ticket 22. The
> `config.{hpp,cpp}` and `tools/config_check` code the note below mentions was deleted with
> the band model (ticket 15).
>
> **Earlier status note (2026-08-17): unimplemented, deferred to crush-depth damage.** The beta
> patcher (`src/patcher_gui/`, commit `9165c5b`) implements none of this — no embedded
> `ape.act` template, no marked struct, no deploy-on-enable or delete-on-revert. That is
> intentional: this ADR exists to solve how APE-owned constants reach a *running* `ape.act`,
> and the only feature that needs one is crush-depth damage, which is not in the beta. Hull
> repair and diesel damage are static value edits to `sh3.exe` and `hsie.ini`, so the beta's
> two site kinds are enough. Revisit when crush-depth damage is picked up.
>
> Two consequences of the deferral, worth naming so they aren't mistaken for oversights:
> the `src/mods/crush_depth_damage/config.{hpp,cpp}` and `tools/config_check` code this ADR
> calls dead is **still built**, because the replacement it assumes doesn't exist yet; and
> the `ape.act` site kind is declared in ticket 08's manifest schema but has no sites and no
> patcher support behind it. See also **ADR-0007**, which records the identity-hash
> verification the beta patcher actually uses.

Ticket 02 (`.scratch/umbrella-roadmap/issues/02-shared-config-file-strategy.md`) killed
`data/Cfg/ape.cfg` outright: the patched binaries are APE's only store, values are read
back out of them to populate the GUI, and there is no sidecar state file. That covers
values that live in `sh3.exe`. It left open how the same "binary is the sole store" model
applies to `ape.act`'s own runtime logic — crush-depth damage is computed live
(HP-per-game-second bands evaluated every loop tick) and cannot be expressed as a static
byte patch to `sh3.exe`, so APE-owned constants still have to reach a *running* `ape.act`
somehow.

This fully supersedes **ADR-0004** (config format and location): there is no config file
of any kind, INI or otherwise, and `src/core/config.{hpp,cpp}` (concretely,
`src/mods/crush_depth_damage/config.{hpp,cpp}` today) plus the `tools/config_check`
harness are dead code once this is implemented — expect deletion, not repurposing.

## Decided

**`ape.act` becomes an artifact the patcher deploys, not one that is already sitting in
the game root waiting to be edited.** APE's C++ build still produces `ape.act`, but as a
*template*: it is embedded as a resource inside the C# patcher exe, not installed by
itself. The patcher copies that template into the game root and patches it only when the
user has at least one runtime feature enabled — a checkbox list with nothing live-computed
enabled means no DLL is installed at all. Unchecking the last such feature deletes
`ape.act` from the game root again, mirroring ticket 02's "revert is just unchecking, no
uninstall flow."

**Constants live in a magic-marked struct baked into `ape.act`'s own `.data`**, one struct
per mod (crush-depth damage gets its own; a future mod gets its own alongside it, not a
shared or versioned mega-struct). APE's build step records each struct's file offset into
the generated manifest (the same manifest ticket 08 defines for `sh3.exe` sites) — the
patcher seeks to that offset in its embedded template copy and verifies the marker before
writing, identical in shape to the context/field verify-before-write split ticket 02
established for `sh3.exe`. No runtime AOB scanning, consistent with ticket 05's dropping
of that technique in favour of build-time-known offsets plus verification.

**Read-back**, ticket 02's other requirement, extends naturally: when a deployed
`ape.act` already exists in the game root, the GUI reads current values from it. When it
doesn't (nothing enabled yet, or the user just reverted), the GUI reads the same
magic-marked struct out of the patcher's own embedded template instead — same offset,
different source file, so the GUI always has something to show.

**`ape.act` verifies its own marker at startup and fails inert on mismatch**, consistent
with the project's existing fail-inert posture (ADR-0003, and the current
`UnresolvedGameState` fallback in `dllmain.cpp`): if the struct's magic/version field
doesn't check out, the loop for that mod simply doesn't run, the same way a missing
`ape.cfg` currently makes it log and return rather than run with garbage values.

## Left open

Whether `sh3.exe`'s dedicated `LoadLibrary` call site for `ape.act` (ADR-0002) tolerates
`ape.act` being absent from the game root has not been RE'd or confirmed — h.sie's and
NSAP's own call sites are fire-and-forget (`DllMain`-only work, no exports, return value
unused elsewhere), which is why ADR-0002 mirrors that shape for `ape.act`, but nobody has
disassembled *this* call site to confirm it discards the result the same way. This is a
dependency for whichever ticket implements the actual `sh3.exe` patch for that call site,
not a blocker for this decision.
