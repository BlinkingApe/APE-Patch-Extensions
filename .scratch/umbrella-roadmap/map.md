# Umbrella roadmap: multi-mod repo structure + candidate goals

Label: wayfinder:map

## Destination

A repo (`src/`, `.scratch/`, `docs/`) reorganized to hold multiple mods/subsystems
under one shared `ape.act` runtime — extensible for future growth — **and** each
of the currently-known candidate goals (hull-repair patch, diesel-damage patch,
docking mod) scoped precisely enough to spin up its own feature effort (PRD +
issues dir), the same way `crush-depth-damage` already works. Reaching the end
means: the layout is decided and applied, and nothing is left to decide before
each new goal can start its own implementation-issue tracker.

## Notes

- Domain: OAE sh3.exe / `.act` DLL modding — see `CONTEXT.md` at repo root.
- One shared `ape.act` runtime, many subsystems (decided) — not separate DLLs
  per mod.
- **APE now has two deliverables, not one** (decided 2026-08-15 via ticket 03):
  `ape.act` for logic that must run live, **plus a shipped GUI patcher `.exe`**
  (hsieOptions style — checkbox per patch, value inputs where applicable) that
  writes static bytes. `ape.cfg` is dropped; settings become patched bytes.
  The destination above still holds, but "scoped precisely enough to spin up its
  own feature effort" now runs *through* the patcher for most goals.
- **RE method: static first.** Diff per-patch `sh3.exe` variants with
  `pefile`+`capstone` before reaching for a debugger — see
  `docs/re-notes/03-utanker-hull-repair-signature.md`. The variant set lives in
  `data/exe_with_hsie_patches/`.
  **Updated 2026-08-15 (ticket 05):** for *flag bytes* specifically, don't ask for
  a variant — `HSIEContents.txt` in the OAE install root names all 22 patches in
  table order and is validated against three independently-diffed flags. Variant
  diffing remains the method for cave/operand work.
- **Read the install, not just the repo.** Ticket 05's biggest wins came from
  `C:\games\OneAlex Extended 1.5.2` — `HSIEContents.txt`, `hsie.ini`, and
  `Support/Stiebler4C_V16B1/`. Check there before treating something as unknown.
- Existing precedent for a fully-specified mod effort: `.scratch/crush-depth-damage/`
  (PRD.md + numbered issues). New goals should land in a directory of the same
  shape once scoped.
- Priority across new goals (hull-repair, diesel-damage, docking) is
  unordered/opportunistic — no fixed sequence.
- Use `/grilling` and `/domain-modeling` for tickets on this map; use
  `/to-spec` or `/to-tickets` once a goal's ticket resolves, to spin up its own
  feature effort under `.scratch/<feature-slug>/`.

## Decisions so far

- [GUI patcher tool: scope](issues/05-gui-patcher-scope.md) — the tool's **shape**
  is settled, its **v1 feature list deliberately deferred** (now ticket 07).
  **APE-only scope** — APE **reads** h.sie's flag table but never writes it, so
  `HsieOptionsSelector` keeps that job; a patch whose h.sie dependency is off is
  shown **disabled with an explanation** rather than silently written into inert
  code. Sites located by **h.sie version allowlist (`v 1.6B1`/`v 1.6E1`) + fixed
  version-keyed offsets + verify-before-write** — runtime AOB scanning dropped,
  demoted to a Python authoring-time proof technique. **Two write targets**
  (`sh3.exe` and `ape.act`) — which confirms
  ticket 06's ⚠ premise, so 06 is alive. Built in **C# WinForms / .NET Framework
  4.x** (single exe, no runtime download), consuming a **generated manifest
  embedded at build time** from the Python RE tooling. **Standalone in the game
  root, zero JSGME interaction ever** — this subsumes ADR-0005's LoadLibrary
  patcher and reverses both ADR-0005 and `CONTEXT.md`'s JSGME glossary entry.
  Large RE bonus: `HSIEContents.txt` in the install root turned out to name **all
  22 h.sie flags in table order**, validated against the three we had derived by
  diffing — folded into `docs/re-notes/03-utanker-hull-repair-signature.md` §2.
- [Hull-repair patch: scope](issues/03-hull-repair-patch-scope.md) — RE **done, not
  deferred**: h.sie's patches live in sections appended to `sh3.exe`, not in
  `hsie.act`, and static diffing of per-patch exe variants locates any patch's
  flag byte without a debugger. U-Tanker does three separable things; hull repair
  is `hull += (rand(0..15)+5) x 0.01 x maxHull` at `0x633633`, writing the same
  `[boat+0xB8]` field crush-depth already anchors. Ships as a **user-settable
  range** (min=max=0 is "off") delivered by a **static GUI patcher** in the style
  of hsieOptions — **not** `ape.cfg`, which is dropped entirely (reverses
  ADR-0004). Site write-up: `docs/re-notes/03-utanker-hull-repair-signature.md`.
- [Repo layout for a multi-mod umbrella](issues/01-repo-layout-for-multi-mod-umbrella.md) —
  `src/mods/<mod_name>/` per subsystem, shared infra in `src/core/`, DLL
  entrypoint stays in `src/ape_act/`; `.scratch/` and `docs/` unchanged.
  Applied: `crush-depth-damage` moved into `src/mods/crush_depth_damage/`,
  `log.hpp` promoted to `src/core/`, CMake targets renamed
  (`crush_depth_damage_core`/`crush_depth_damage_config`). Build + tests +
  `config_check` verified green.

## Not yet specified

- **Pseudo-dock mod (item 4).** Docking triggers "other events in-game" — still
  too open-ended to ticket (the candidate trigger events remain unnamed, and that
  is the blocker, not the mechanism). But the fog thinned considerably on
  2026-08-15: ticket 03's RE found h.sie's **docking detection already built** —
  the cave at `0x633743` matches the mission-order string `"Dock at: U-"` /
  `"Anlegen in: U-"` and sets `[0x63E168] = 1`, which downstream caves consume.
  A pseudo-dock mod likely hangs new behavior off that same variable rather than
  detecting docking itself. Probably still wants a grilling/prototype session to
  name the events.
- **Future/unnamed sub-projects.** The user wants the structure extensible for
  goals not yet conceived. No specific ticket — the layout ticket (01) should
  produce a convention general enough to absorb these without another reorg.
- **Retiring the runtime config layer.** Dropping `ape.cfg` means ADR-0004 needs
  superseding and `src/core/config.{hpp,cpp}` + its `config_check` target need
  deleting or repurposing. Ticket 06 decides the mechanism; the actual removal is
  execution work that follows, and may want its own effort rather than a ticket
  here.
- **The `0x5543DC` singleton.** Read by both the u-tanker mod's block 1 and the
  diesel-damage cave, and listed as an unidentified guard in
  `01-crush-depth-damage-signature.md` §2. Identifying it once would serve three
  efforts; too small to ticket on its own, likely absorbed by whichever comes
  first.
- **Documentation debt from ticket 05's reversals.** Three documents now state
  things the map has decided against: `CONTEXT.md`'s JSGME glossary entry
  ("APE packages its end-user deliverable as a JSGME-installable folder"),
  ADR-0005 (whose whole premise is a JSGME package), and `src/patcher/main.cpp`
  plus its CMake target (superseded by the C# tool). Two new ADRs are also owed —
  the C#/C++ toolchain split, and the manifest generation step. This is execution
  work following settled decisions, not a decision itself; likely folded into
  whichever effort starts building the patcher.
- **Does `hsie.ini` still do anything?** h.sie's numeric parameters sit as a float
  array at VA `0x63C074`, but the live values don't obviously correspond to
  `hsie.ini`'s (`Val1=60.0`, `Val2=0.5` vs `3.5, 1.0, …`). Whether the ini is
  applied at patch time, read live by `hsie.act`, or simply stale is unknown.
  Only matters if APE ever wants to *read* h.sie's settings; not on the route yet.
- **Which builds are supported, and is `4B6BEE20` still one of them?** Four
  installs are now surveyed (`docs/re-notes/oae-builds.md`) spanning two h.sie
  versions, and **none of them is the registered `4B6BEE20`**. Ticket 08 decides
  the bar for admitting a version to the allowlist; what it does *not* settle is
  which of OAE 1.5.2 (`v 1.6E1`) and OAE 1.5.3.7 (`v 1.6B1`) is the user's actual
  play target, which matters because all RE to date was done against the
  `v 1.6E1`-sized variant set.
- **Is the "GWX 3.0 Gold" exe genuine?** It is byte-identical to OAE 1.5.2's
  across stock `.text` and h.sie `.pcode`, differing only in flag bytes (all
  `0x00`), and the folder has no `hsie.act` or `HSIEContents.txt`. Probably an
  OAE-derived binary sitting in a GWX folder. Until confirmed it must not be
  counted as independent evidence of cross-suite stability — which weakens the
  four-install survey to a three-install one.

## Out of scope

- **APE replacing or subsuming hsieOptions.** Re-exposing h.sie's 22 flag bytes as
  checkboxes is technically trivial, but ruled out of APE's scope by
  [GUI patcher tool: scope](issues/05-gui-patcher-scope.md): APE presents only its
  own features and never the flag table, not even read-only.
  `HsieOptionsSelector` remains the owner of h.sie's own toggles. Taking on 22
  patches APE hasn't reverse-engineered sits past this map's destination.
  (Previously carried in **Not yet specified**; graduated to a decision, then out.)
