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
  `data/exe_with_hsie_patches/`; ask the user for a new variant to identify any
  other patch's flag byte.
- Existing precedent for a fully-specified mod effort: `.scratch/crush-depth-damage/`
  (PRD.md + numbered issues). New goals should land in a directory of the same
  shape once scoped.
- Priority across new goals (hull-repair, diesel-damage, docking) is
  unordered/opportunistic — no fixed sequence.
- Use `/grilling` and `/domain-modeling` for tickets on this map; use
  `/to-spec` or `/to-tickets` once a goal's ticket resolves, to spin up its own
  feature effort under `.scratch/<feature-slug>/`.

## Decisions so far

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
- **Does APE's patcher replace hsieOptions?** Re-exposing h.sie's own 20 flag
  bytes as checkboxes is now trivially possible. Whether APE *should* — versus
  staying a narrow add-on beside h.sie's tool — is a product question flagged in
  ticket 05 but not yet sharp enough to stand alone.
- **The `0x5543DC` singleton.** Read by both the u-tanker mod's block 1 and the
  diesel-damage cave, and listed as an unidentified guard in
  `01-crush-depth-damage-signature.md` §2. Identifying it once would serve three
  efforts; too small to ticket on its own, likely absorbed by whichever comes
  first.

## Out of scope

(none yet)
