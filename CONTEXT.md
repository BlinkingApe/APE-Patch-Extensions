# APE (Ape's Patch Extensions)

APE is a workflow and tool suite for developing and maintaining runtime engine hacks for a specific, heavily-modified Silent Hunter 3 executable. It works by running its own logic *alongside* the game — reading and writing game memory from an injected module — rather than intercepting, replacing, or routing through stock game functions, and without shipping a rewritten engine or a fully replaced executable.

## Language

**OAE sh3.exe**:
The specific, already-modified `sh3.exe` binary APE targets — OneAlex Extended, which itself bundles h.sie's hardcode patches, Stiebler's mods, and NSAP (Night Surface Attack Project). The underlying `sh3.exe` is the stock Ubisoft image and is never recompiled; OAE and its upstream mods are byte-level patches reapplied over it (new default values, inserted call sites) as they update. APE still resolves its value anchors dynamically rather than trusting fixed addresses, so a patch update that shifts an anchor is detected rather than silently misread.
_Avoid_: "the game", "stock sh3.exe" (stock refers to the unmodified Ubisoft release — a different binary with different code layout)

**.act module**:
A plain PE32 DLL, loaded by OAE sh3.exe's own custom module loader (a hand-patched `LoadLibrary` call site) rather than the standard Windows PE import table, under the `.act` file extension. Distinct from unrelated `.act` palette/color-table data files that also exist elsewhere in the install (e.g. terrain palettes) — same extension, unrelated format.
_Avoid_: "plugin", "act file" (ambiguous with the palette data files)

**crush depth**:
An existing, engine-tracked value representing the depth at which a submarine begins taking hull damage from pressure. APE reads this value; it does not define or own it.

**damage window** (also: **band**):
A configured range, expressed as a fraction of crush depth past the threshold, within which a fixed HP-per-game-second rate applies. APE defines three: light, medium, and an open-ended heavy band. APE owns the entire crush-depth damage model — stock's own bleed is neutralized (`CrashSpeed`=0 in shipped per-unit data) — so there is no "hand back to stock" zone; the heavy band applies until the hull is gone.
_Avoid_: "tier", "zone"

**AOB pattern scan** (array-of-bytes scan):
Resolving an address at runtime by searching loaded process memory for a byte signature (with wildcards for bytes expected to vary), rather than trusting a fixed offset found once and hardcoded. APE uses it to anchor the pointer chains it follows to the live values it reads/writes (player boat, depth, crush depth, HP, mission clock). Since stock sh3.exe never moves, the payoff is self-verification: a patch that shifts an anchor makes the scan miss, and APE fails inert rather than misreading.
_Avoid_: "hardcoded offset" (the alternative APE deliberately avoids for its anchors)

**JSGME**:
Generic Mod Enabler — the mod installation tool already standard in this modding ecosystem. APE packages its end-user deliverable as a JSGME-installable folder.
