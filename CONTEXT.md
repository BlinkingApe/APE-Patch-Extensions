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
An existing, engine-tracked value representing the depth at which stock SH3 begins taking hull damage from pressure. **APE's depth-pressure-stress feature never reads this value** — it is superseded terminology from an earlier (abandoned) model. Stock's own crush-depth damage is left untouched below it.
_Avoid_: "CDT" as something APE resolves or owns (only the abandoned model did).

**depth tier**:
One of a small number of configured **absolute** depth thresholds (default ~100m for tier 1, deeper for tier 2) below which APE's own effects apply: a hull-damage **multiplier** (poll-and-amplify — APE notices hull HP fell by N since last tick and subtracts a further `(mult−1)·N`, never attributing the cause), a flat astern **seep rate**, and a flat electric-engine **wear chance**. Crossing a tier boundary is a step change. Tier 3 is stock's own crush-depth damage, untouched — there is no tier APE owns entirely. See `.scratch/depth-pressure-stress/map.md`.
_Avoid_: "damage window", "band" (terminology from the abandoned three-band, fraction-of-crush-depth model)

**AOB pattern scan** (array-of-bytes scan):
Resolving an address at runtime by searching loaded process memory for a byte signature (with wildcards for bytes expected to vary), rather than trusting a fixed offset found once and hardcoded. APE uses it to anchor the pointer chains it follows to the live values it reads/writes (player boat, depth, hull HP, mission clock, and per-feature values like the diesel/electric-engine damage floats). Since stock sh3.exe never moves, the payoff is self-verification: a patch that shifts an anchor makes the scan miss, and APE fails inert rather than misreading.
_Avoid_: "hardcoded offset" (the alternative APE deliberately avoids for its anchors)

**JSGME**:
Generic Mod Enabler — the mod installation tool already standard in this modding ecosystem. APE packages its end-user deliverable as a JSGME-installable folder.
