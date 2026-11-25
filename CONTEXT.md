# APE (Ape's Patch Extensions)

APE is a workflow and tool suite for developing and maintaining runtime engine hacks for a specific, heavily-modified Silent Hunter 3 executable. It works by running its own logic *alongside* the game — reading and writing game memory from an injected module — rather than intercepting, replacing, or routing through stock game functions, and without shipping a rewritten engine or a fully replaced executable.

## Language

**OAE sh3.exe**:
An already-modified `sh3.exe` binary bundling h.sie's hardcode patches — OneAlex Extended (OAE) is the primary reference bundle APE's RE work is derived against, but APE does not target one specific build or distribution. The GUI patcher accepts any `sh3.exe` with a readable `hsie.ini` and h.sie version stamp — OAE and KC (Kriegsmarine Command) are two known bundles that qualify — and never gates on a whole-file identity or version allowlist (ADR-0009); it trusts that h.sie's own installer already refused to run against an `sh3.exe` it doesn't support. The underlying `sh3.exe` is the stock Ubisoft image and is never recompiled; OAE/KC and their upstream mods are byte-level patches reapplied over it (new default values, inserted call sites) as they update. APE's runtime resolution (`ape.act`, following pointer chains to live values in a running process, not yet shipped) still resolves its value anchors dynamically rather than trusting fixed addresses, so a patch update that shifts an anchor is detected rather than silently misread. The GUI patcher's static edits to files on disk are a separate case with a different, legitimate fixed-offset-per-site model, verified at write time rather than by build identity — see ADR-0007, narrowed by ADR-0009.
_Avoid_: "the game", "stock sh3.exe" (stock refers to the unmodified Ubisoft release — a different binary with different code layout)

**.act module**:
A plain PE32 DLL, loaded by OAE sh3.exe's own custom module loader (a hand-patched `LoadLibrary` call site) rather than the standard Windows PE import table, under the `.act` file extension. Distinct from unrelated `.act` palette/color-table data files that also exist elsewhere in the install (e.g. terrain palettes) — same extension, unrelated format.
_Avoid_: "plugin", "act file" (ambiguous with the palette data files)

**crush depth**:
The depth at which stock SH3 begins taking hull damage from pressure, right now. It is 70% of the rated crush depth at full hull and gets shallower as the hull is damaged, so a damaged boat starts crushing higher up. It is the floor of tier 2; below it is tier 3 (ADR-0011).
_Avoid_: "CDT" as something APE owns (only the abandoned model did); "crush depth" for the per-class `.zon` figure (that is the **rated crush depth**).

**rated crush depth**:
A boat class's crush depth as its data states it (the `CrashDepth` value in each boat's `.zon` data). Not where damage begins: the live crush depth is shallower, 70% of it at full hull.

**crush speed**:
Stock's per-boat-class scale for hull damage below crush depth (the `CrashSpeed` value in each boat's `.zon` data). Damage isn't flat: it grows the deeper the boat sits below crush depth and the more damaged its hull, and equals the crush speed in HP per second only at the rated crush depth at full hull (ADR-0011).

**crush time**:
The player's chosen nominal number of seconds a boat survives below crush depth from full hull. APE turns it into each U-boat class's crush speed (`Hit_Points ÷ crush time`), so every class crushes on the same scale. Nominal: just past crush depth a boat lasts somewhat longer, at its rated crush depth about a third as long. Not affected by the hull multiplier.

**depth tier**:
One of APE's absolute-depth bands of depth stress. Tier 1 runs from 100 m to the tier-2 depth; tier 2 from the player's tier-2 depth to crush depth. Each carries its own **hull multiplier** and **seepage** rate. (**Motor flooding damage** is not tied to a tier.) Crossing a boundary is a step change. Tier 3 (below crush depth, wherever damage has lifted it, even above tier 1 or 2) adds stock's crush damage, at the crush speed APE sets; seepage and the hull multiplier carry on at tier-2 values, except that crush damage itself is never multiplied. See `.scratch/depth-pressure-stress/map.md`.
_Avoid_: "damage window", "band" (terminology from the abandoned three-band, fraction-of-crush-depth model)

**hull multiplier**:
A depth tier's amplification of hull damage: APE notices hull HP fell by N since its last look and takes a further `(mult−1)·N`, whatever the cause — except crush damage below crush depth, which is never multiplied.

**seepage**:
Water APE adds to the electric engine room while the boat is in a depth tier, on every U-boat class, which the crew must pump out. Shown to players as "Leakage".
_Avoid_: "leak rate", "flooding" (flooding is stock's own, from hull breaches)

**motor flooding damage**:
Damage APE adds to both electric engines, at a steady rate per game-second, while the water in their room is at or above the player's flooding threshold and the machine telegraph is not at stop, at any depth and whatever flooded the room. Never pushed past the point where the crew can still repair it. Ordering stop, or pumping the room below the threshold, ends it. Shown to players as "Motor damage".
_Avoid_: "electrical engine damage" (damage is stock's, from hits); "electric-engine wear" (the superseded random-hit model)

**AOB pattern scan** (array-of-bytes scan):
Resolving an address at runtime by searching loaded process memory for a byte signature (with wildcards for bytes expected to vary), rather than trusting a fixed offset found once and hardcoded. `ape.act` uses it to anchor the pointer chains it follows to the live values it reads/writes (player boat, depth, hull HP, mission clock, and per-feature values like the diesel/electric-engine damage floats). Since stock sh3.exe never moves, the payoff is self-verification: a patch that shifts an anchor makes the scan miss, and APE fails inert rather than misreading.
_Avoid_: "hardcoded offset" for `ape.act`'s runtime anchors — the alternative it deliberately avoids. Not a project-wide rule: the GUI patcher's static file edits legitimately use fixed offsets, verified per-site by context bytes at write time rather than a whole-file identity check (ADR-0007, narrowed by ADR-0009).

**JSGME**:
Generic Mod Enabler — the mod installation tool already standard in this modding ecosystem. APE packages its end-user deliverable as a JSGME-installable folder.
