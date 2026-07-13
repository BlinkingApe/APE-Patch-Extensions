# APE (Ape's Patch Extensions)

APE is a workflow and tool suite for developing and maintaining runtime engine hacks for a specific, heavily-modified Silent Hunter 3 executable. It works by intercepting and replacing existing game logic at runtime, rather than shipping a rewritten engine or a fully replaced executable.

## Language

**OAE sh3.exe**:
The specific, already-modified `sh3.exe` binary APE targets — OneAlex Extended, which itself bundles h.sie's hardcode patches, Stiebler's mods, and NSAP (Night Surface Attack Project). Rebuilt occasionally as those upstream mods update, which is why APE resolves hook targets dynamically rather than trusting a fixed address.
_Avoid_: "the game", "stock sh3.exe" (stock refers to the unmodified Ubisoft release — a different binary with different code layout)

**.act module**:
A plain PE32 DLL, loaded by OAE sh3.exe's own custom module loader (a hand-patched `LoadLibrary` call site) rather than the standard Windows PE import table, under the `.act` file extension. Distinct from unrelated `.act` palette/color-table data files that also exist elsewhere in the install (e.g. terrain palettes) — same extension, unrelated format.
_Avoid_: "plugin", "act file" (ambiguous with the palette data files)

**crush depth**:
An existing, engine-tracked value representing the depth at which a submarine begins taking hull damage from pressure. APE reads this value; it does not define or own it.

**damage window** (also: **band**):
A configured range, expressed as a percentage of crush depth, within which a fixed damage-per-tick rate applies. APE currently defines three: light, medium, heavy. Beyond the last configured window, stock damage behavior takes over unmodified.
_Avoid_: "tier", "zone"

**AOB pattern scan** (array-of-bytes scan):
Resolving a function's address at runtime by searching loaded process memory for a byte signature (with wildcards for bytes expected to vary), rather than trusting a fixed offset found once and hardcoded. APE's primary technique for tolerating OAE sh3.exe rebuilds.
_Avoid_: "hardcoded offset" (the alternative APE deliberately avoids for hook targets)

**JSGME**:
Generic Mod Enabler — the mod installation tool already standard in this modding ecosystem. APE packages its end-user deliverable as a JSGME-installable folder.
