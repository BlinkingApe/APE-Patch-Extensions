# Distribution: a patcher tool applies the sh3.exe change, not a redistributed pre-patched exe

APE is intended to eventually be shared with the OAE/SH3 modding community, not used only personally. JSGME — the ecosystem's standard mod-enabler — only adds/removes whole files; it can't binary-patch an existing file like `sh3.exe` in place.

> **Status note (2026-08-17): `ape.act` deployment deferred.** The beta patcher
> (`src/patcher_gui/`, commit `9165c5b`) ships neither `ape.act` nor a JSGME package, and
> writes no `LoadLibrary` call site. It patches value sites in `sh3.exe` and keys in
> `hsie.ini` only. Nothing here is retracted — hull repair and diesel damage are static
> value edits that need no runtime DLL, so the `ape.act` half of this decision only becomes
> necessary when crush-depth damage is implemented (see `.scratch/crush-depth-damage/`).
> Revisit this ADR then; until then it describes the intended end state, not the shipped one.

Decided: the JSGME-installable package contains only `ape.act` and `data/Cfg/ape.cfg`. The `sh3.exe` `LoadLibrary` call site (see ADR-0002) is applied by a standalone patcher tool that end users run once against their own OAE `sh3.exe`. This avoids ever redistributing modified copyrighted binary bytes, and the same tool doubles as the internal tool used to reapply the patch after OAE rebuilds during development. Whether to also offer a pre-patched exe as an alternative distribution path is deliberately left open for later.
