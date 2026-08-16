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
  **Ticket 04 doubled down:** `hsie.ini` states the diesel damage formula in a
  comment, and its `AdrN` table maps every parameter to its address. Also mine
  `data/Menu/en_menu.txt` — h.sie's `push <id>; call 0x6371D4` message calls
  resolve to plain strings there, and two of them settled what the diesel cave
  was doing.
- **Ask the user for h.sie's manual before concluding a mod lacks a feature.**
  Ticket 04's first pass concluded from the bytes that there was no timing
  component; h.sie's V16 manual §21 has a whole T50 table. Both were true, but
  the user had the document and the agent did not. When a mod's *intent* is in
  question, the docs are primary — and the user has them.
- **Check the precision of the code path before committing to a constant.** Ticket
  04's reopening picked a probability ~5,000× smaller than h.sie's, which put it
  under float32 epsilon; one `fstp dword` in a helper quantized it onto a lattice
  and made everything below `2.98e-8` silently do nothing. Nothing in the formula
  or the docs hints at this — only disassembling the arithmetic does. **Any h.sie
  parameter retuned by more than an order of magnitude needs its code path checked
  for representability**, not just its documented range.
- Existing precedent for a fully-specified mod effort: `.scratch/crush-depth-damage/`
  (PRD.md + numbered issues). New goals should land in a directory of the same
  shape once scoped.
- Priority across new goals (hull-repair, diesel-damage, docking) is
  unordered/opportunistic — no fixed sequence.
- Use `/grilling` and `/domain-modeling` for tickets on this map; use
  `/to-spec` or `/to-tickets` once a goal's ticket resolves, to spin up its own
  feature effort under `.scratch/<feature-slug>/`.

## Decisions so far

- [`hsie.ini` clobbers `.pconst` at load — what does the patcher do about it?](issues/09-hsie-ini-runtime-clobber.md)
  — **No blanket redirect — decided per-site by where the write actually
  sticks.** Diesel damage's own two fields prove why: the **rate** (`Val9`–
  `Val16`) is inside the clobber list, so it goes to **`hsie.ini`**'s keys (a
  `.pconst` write there would be silently overwritten at load and do nothing);
  the **damage range** is four `.pcode` immediates **outside** the clobber
  list, with no `hsie.ini` key at all, so it goes to **`sh3.exe`** directly.
  Each field is written to exactly its one effective store, never mirrored
  into an inert one. **The clobber list itself is hardcoded per h.sie version
  in the manifest**, authored from a real `hsie.ini` like every other
  version-keyed fact — not parsed live — and exists as an authoring/build-time
  check, not a runtime lookup. **`hsie.ini` becomes a third manifest write
  target** alongside `sh3.exe` and `ape.act`, keyed by `ValN=` line match (not
  byte offset), storing the **complete original line** for revert; a
  comment-preserving, key-surgical INI writer is now a required patcher
  component. **Ticket 02's "binary is the only store" is restated**: each
  written value has exactly one effective store, recorded per-site in the
  manifest — `sh3.exe`, `ape.act`, or now `hsie.ini`. Whether anything besides
  `hsie.act` overlays values at runtime is left as fog, not a ticket — nothing
  currently needs that answered.
- [How patcher-set values reach ape.act's runtime logic](issues/06-patcher-to-ape-act-values.md)
  — **`ape.act` becomes a patcher-deployed artifact, not a pre-installed one.**
  APE's build still produces it, but as a **template embedded as a resource in
  the C# patcher exe**; the patcher copies it into the game root and patches it
  **only when a runtime feature is enabled**, and **deletes it again** when the
  last one is unchecked (mirrors ticket 02's "revert is just unchecking").
  Constants live in a **magic-marked struct in `ape.act`'s own `.data`, one per
  mod** — not read from `sh3.exe` — located via a **build-recorded offset in the
  manifest**, verified before write like every other site. **Read-back** falls
  back to the patcher's own embedded template when no `ape.act` is deployed yet.
  `ape.act` **verifies its own marker at startup and fails inert on mismatch**
  (ADR-0003 posture). `config.{hpp,cpp}` and `tools/config_check` are confirmed
  **dead, not repurposed**. ⚠ Left open: whether `sh3.exe`'s `LoadLibrary` call
  site for `ape.act` (ADR-0002) tolerates a missing file — flagged for whichever
  ticket implements that patch, not resolved here. Superseding ADR:
  `docs/adr/0006-ape-act-owned-constants-live-in-a-marked-struct-the-patcher-deploys.md`.
- [Settings model for the GUI patcher](issues/02-shared-config-file-strategy.md) —
  **`ape.cfg` is genuinely gone, not renamed.** The patched binaries are the only
  store: the patcher **reads current values back out of `sh3.exe`/`ape.act`** to
  populate the GUI, exactly as `HsieOptionsSelector` does with h.sie's sliders,
  and keeps **no sidecar state file**. Each manifest site splits into **invariant
  context bytes** (verified before write) and a **mutable field** (read back,
  overwritten) — which is what lets verification and user-editable values coexist.
  **Revert is just unchecking**: no uninstall flow and no whole-file backup — the
  same way a user undoes a h.sie edit. Surgical, so hsieOptions choices made later
  survive. ⚠ "APE off" (original bytes restored, h.sie's stock behaviour returns)
  is **not** the same state as "feature off" (APE applied, range set to zero).
  UI groups by feature, never by write target. Fully supersedes ADR-0004 and kills
  `src/core/config.{hpp,cpp}` outright.
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
- [Diesel-damage patch: scope](issues/04-diesel-damage-patch-scope.md) —
  **reopened and re-resolved 2026-08-15** on a new premise from the user (via a
  historian): a **very low chance of damage anytime the diesels run, at any
  speed** — once or twice per ~10,000-hour career. This **flattens h.sie's
  formula to a single constant** (Exponent `0.0`, Factor `1.0` for all damage
  counts, so `p_ds = p_dso`), and dissolves the previous resolution's eight-hour
  overload gate along with **the save/reload persistence problem** and **all
  runtime flag writing**. ⚠ **A float32 trap in `0x635818`** rounds `(1-p)` onto a
  2⁻²⁴ lattice, flooring the constants-only rate at 2.15 events/career and making
  anything below `2.98e-8` **silently inert** — so APE **rewrites that function**
  (21 bytes into 38) as a clamped `p × TC`, restoring the user's `1.5`/career
  target and continuous tuning. Damage amount collapses to a **single
  `rand(0.15…0.55)` band** on the historian's "must stay repairable" constraint,
  replacing h.sie's escalated `0.30…1.00` which destroys outright ~43% of the
  time. Ships via **two write targets** — 8 `ValN` keys in `hsie.ini` plus 5
  `.pcode` sites in `sh3.exe` — with **rate and damage band as GUI input fields**.
  No `ape.act` code and **no separate `.scratch/` effort**: it is a line item in
  ticket 07, and now the candidate exercising the most of the patcher's
  architecture. Site write-up: `docs/re-notes/04-diesel-damage-signature.md`.
- [May APE write h.sie's flag table in live memory?](issues/10-ape-writing-hsie-flags-at-runtime.md)
  — **moot, closed without adjudication.** The conflict existed only because
  ticket 04's first resolution needed APE to flip `0x63CF13`; the reopened ticket
  removes that mechanism entirely. **Ticket 05's rule stands unmodified** — APE
  reads h.sie's flag table and never writes it. The boundary was vacated, not
  argued, so reopen this rather than citing it if a future feature needs runtime
  flag writing.
- [Repo layout for a multi-mod umbrella](issues/01-repo-layout-for-multi-mod-umbrella.md) —
  `src/mods/<mod_name>/` per subsystem, shared infra in `src/core/`, DLL
  entrypoint stays in `src/ape_act/`; `.scratch/` and `docs/` unchanged.
  Applied: `crush-depth-damage` moved into `src/mods/crush_depth_damage/`,
  `log.hpp` promoted to `src/core/`, CMake targets renamed
  (`crush_depth_damage_core`/`crush_depth_damage_config`). Build + tests +
  `config_check` verified green.

## Not yet specified

- **Does `sh3.exe`'s `LoadLibrary` call site for `ape.act` tolerate a missing
  file?** Surfaced 2026-08-16 by ticket 06: reverting APE's last runtime
  feature now deletes `ape.act` from the game root, which only works if that
  call site is fire-and-forget like h.sie's/NSAP's own (ADR-0002 assumes this
  by design but nobody has disassembled *this* specific site to confirm it).
  Not a ticket yet because implementing the `sh3.exe` patch for that call site
  is itself still unticketed execution work — this rides along with whichever
  effort builds it, not the current frontier.
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
- **The `0x5543DC` singleton.** Read by the u-tanker mod's block 1, the
  diesel-damage cave, and listed as an unidentified guard in
  `01-crush-depth-damage-signature.md` §2 — plus **55 xrefs in stock `.text`**, so
  it is an engine-level object, not a h.sie invention. Ticket 04 added two more
  unknown fields on it (`+0x18`/`+0x1C`, summed and tested against zero — a
  surfaced check?) and confirmed `+4→+0x30C` is the two-diesel damage pair and
  `+4→+0x320` the machine telegraph. Identifying it once would serve three
  efforts; still too small to ticket, likely absorbed by whichever comes first.
- ~~**Persisting APE state across save/reload.**~~ **Dissolved 2026-08-15** by
  ticket 04's reopening. It was fog only because the eight-hour overload
  accumulator would have been trivially save-scummed; the flat wear model is
  memoryless and holds no state at all. **No live feature needs a persistence
  mechanism.** Kept as one line because the underlying gap is real — APE still has
  no save-game hook and no sidecar state file (ticket 02 killed `ape.cfg`) — so
  the next feature that wants durable state re-raises this from scratch.
- **Documentation debt from ticket 05's reversals.** Three documents now state
  things the map has decided against: `CONTEXT.md`'s JSGME glossary entry
  ("APE packages its end-user deliverable as a JSGME-installable folder"),
  ADR-0005 (whose whole premise is a JSGME package), and `src/patcher/main.cpp`
  plus its CMake target (superseded by the C# tool). Two new ADRs are also owed —
  the C#/C++ toolchain split, and the manifest generation step. This is execution
  work following settled decisions, not a decision itself; likely folded into
  whichever effort starts building the patcher.
- ~~**Does `hsie.ini` still do anything?**~~ **Answered 2026-08-15 by ticket 04 —
  and it is on the route after all.** `hsie.act` reads `./hsie.ini` at load,
  `VirtualProtect`s `.pconst`, and writes 24 floats over `0x63C000`–`0x63C0E8`
  from the ini's `ValN`/`AdrN` table. Proven by disagreement: the exe holds
  0.15/0.10/0.03 where the ini holds 0.35/0.20/0.05, and the ini wins in play.
  This is not a curiosity — it means **static patcher writes to those 24
  addresses are silently overwritten at runtime, and verify-before-write cannot
  detect it.** Graduated to ticket 09.
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
  ~~⚠ Under tension since ticket 04.~~ **Tension gone, 2026-08-15.** The reopened
  ticket 04 no longer writes `0x63CF13` or any other flag, so
  [ticket 10](issues/10-ape-writing-hsie-flags-at-runtime.md) closed as moot and
  this line stands exactly as originally written.
