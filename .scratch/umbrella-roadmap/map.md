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

**Status 2026-08-16 — reached. No open tickets remain.** The layout was applied
(ticket 01) and the last open decision closed with ticket 07. Hull repair and
diesel damage are scoped and discharged together into one new effort,
`.scratch/gui-patcher/`, which is where work continues — starting with a
`/prototype` session. The docking mod remains fog and would be charted as a fresh
effort if picked up. Everything still listed below under **Not yet specified** is
either deferred past v1 or belongs to another effort; none of it blocks work
starting.

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
- **Hash the running exe before trusting a live CE session against RE notes.**
  Ticket 11's live session (2026-08-16) burned real playtime on addresses that
  never resolved sensibly, including a breakpoint on a static VA that didn't
  fire despite the exact behavior it should gate being observed moments later —
  and the running `sh3.exe`'s build was never checked against the one the notes
  were derived from (`01-live-session-playbook.md` names it and says explicitly
  to re-verify if the hash doesn't match). **Confirm the SHA-256 first**, every
  session, before spending CE time on absolute VAs from these notes.
- Existing precedent for a fully-specified mod effort: `.scratch/crush-depth-damage/`
  (PRD.md + numbered issues). New goals should land in a directory of the same
  shape once scoped.
- Priority across new goals (hull-repair, diesel-damage, docking) is
  unordered/opportunistic — no fixed sequence.
- Use `/grilling` and `/domain-modeling` for tickets on this map; use
  `/to-spec` or `/to-tickets` once a goal's ticket resolves, to spin up its own
  feature effort under `.scratch/<feature-slug>/`.

## Decisions so far

- [Patcher v1 scope: which patches ship first](issues/07-patcher-v1-scope.md) —
  **v1 is hull repair + diesel damage, on two write targets and one build.**
  Hull repair goes **first as the tracer bullet** (one `sh3.exe` operand range,
  no derived values, no INI writer); diesel damage follows and brings the INI
  writer, the 21-byte `fixed-rewrite`, the derived `Val10`, and ticket 05's
  disabled-with-explanation path. **`ape.act` is deferred whole** — its only
  candidate, crush-depth constants, is blocked on crush-depth issue 01's four
  *dynamic* CE hunts, so including it would gate the release on unfinished RE
  rather than on build effort. **This drops the LoadLibrary call-site patch from
  v1 too** (nothing loads `ape.act`), retiring ticket 06's ⚠ as a v1 concern.
  Allowlist is **exactly one build — OAE 1.5.2 / `v 1.6E1`**, the one every site
  was derived on and ticket 12 was played on; everything else refuses to write.
  **One new effort, `.scratch/gui-patcher/`**, covers the tool, the manifest
  generator, and both features — neither feature carries a PRD alone. Its first
  move is a **`/prototype` session**, not implementation: ticket 05 settled the
  architecture but nothing has settled what the tool is like to *use*, and the
  diesel panel's two fields have consequences illegible from the inputs.
  **Done bar: the shipped patcher writes both features on the real install,
  effects observed in play, unchecking restores bytes identical to the backups** —
  ticket 12 proved the levers by hand, which is a different claim from proving
  the tool. ⚠ That install is still in ticket 12's test state and must be
  reverted before it can be a clean baseline. Deferred to a later version, not
  out of scope: `ape.act` + crush-depth constants, the LoadLibrary patch,
  `v 1.6B1`, docking.

- [Black-box test: prove APE controls diesel-damage magnitude and timing](issues/12-diesel-levers-blackbox-test.md)
  — **PASS, in play, 2026-08-16.** Band forced to `rand(1.0, 1.0)` in `sh3.exe`
  `.pcode` + rate to `p_ds = 0.01`/game-second in `hsie.ini` → diesel destroyed
  outright at **~6 in-game minutes**, an outcome h.sie's stock first-hit band
  (`0.01…0.59`) *cannot* produce, so the result is only reachable via APE's
  written bytes. **Both levers are APE's.** Two consequences beyond the levers:
  **ticket 09's per-site store routing is confirmed on a real install** — one
  feature, two stores (`hsie.ini` keys for the clobbered rate, `.pcode`
  immediates for the band), both stuck; and the **hash-first discipline paid
  off** — the backup matched `51d14ca3…` exactly, the live exe differed by
  precisely the Diesel Damage flag byte, and every address from the RE notes
  resolved first try, the exact opposite of ticket 11's 2nd session.
  **Ticket 07's blocker on diesel damage is lifted.** Not proven, and
  deliberately: the rate *curve* (one sample only — and none needed, since rate
  ships as a user field), carried to fog.
  **Four further conditions were then played in the same session, and returned
  two findings bigger than the test itself:**
  **(A) `pow(0, 0) = 1` — ticket 04's flat exponent fires at full rate at all
  stop.** Surfaced + all stop destroyed an engine; the model has no idle case at
  all. Fixed without any code patch by pairing a *small positive* exponent with
  a derived threshold — `Val14/15/16 = 0.01`, `Val10 = p_dso × 0.977237` — which
  turns h.sie's existing `p_ds > Val10` test into an exact **telegraph > 0.1**
  cutoff while keeping the rate flat to **1.6%** across 0.20→1.00. Clears OAE's
  slowest telegraph (0.20) and KC's (0.40). ⚠ This makes **`Val10` the first
  *derived* manifest site** — recomputed from the rate field on every write, not
  user-facing (tickets 07/08). Ticket 04 amended.
  **(B) The surfaced gate is real, works, and counts snorkelling** — see the
  fog entry below; this substantially retires ticket 11's accepted risk.
  **Both then confirmed in play**: the idle cutoff suppresses all stop while
  every real telegraph setting still damages, and **a destroyed (`1.0`) diesel
  cannot be repaired at sea** — answering ticket 11's last side question and
  turning ticket 04's "damage must stay repairable" from a preference into a
  proven constraint. ⚠ Left for the feature's own tuning work: APE's
  `0.15…0.55` band still reaches `1.0` on two unrepaired hits 3.1% of the time,
  now known to cost an engine for the patrol.
  ⚠ The OAE 1.5.2 install is **left in test state** — revert from `sh3_BACKUP.exe` /
  `hsie_BACKUP.ini` before playing normally.

- [Prove the surfaced gate before shipping always-on diesel wear](issues/11-surfaced-gate-proof.md)
  — **resolved by scope pivot, not by proof.** Two sessions of RE (static
  exhausted; live broke on an unverified binary hash and a breakpoint that
  never fired) hit diminishing returns on the gate's identity
  (`[0x5543DC+0x18]`/`+0x1C` at `0x6350CA`). Decision: stop chasing RE proof,
  trust h.sie's developer got the gate and damage model right, and replace
  the pre-ship check with a narrower, directly testable claim — that APE's
  own patched values (damage magnitude, timing) take effect as intended,
  independent of the gate debate. Risk accepted explicitly, not dismissed:
  the frozen, identical gate-field readings observed live are suggestive of
  a dead/always-pass gate, just not confirmed. Carried forward in **Not yet
  specified**. Replacement pre-ship gate is
  [ticket 12](issues/12-diesel-levers-blackbox-test.md).

- [Manifest format and the version allowlist](issues/08-signature-manifest-format.md)
  — **two-level schema: a patch groups sites, each site is one field with one
  store.** Patch carries id, display name, and an optional h.sie-flag
  dependency (drives ticket 05's disabled+explained gating). Site carries id,
  `store` (`sh3.exe`/`ape.act`/`hsie.ini`), `field encoding`
  (`byte`/`float32`/`operand-immediate`/**`fixed-rewrite`**, the last added
  mid-ticket for diesel-damage's 21-byte function rewrite, which has no
  tunable value at all), valid range + default (absent for `fixed-rewrite`),
  invariant context bytes, and the complete original byte run/line for exact
  revert. **Locator shape is kind-specific**: `sh3.exe` gets a **per-site
  per-version offset map** (option A — one manifest, not one file per
  version); `ape.act` gets a single build-recorded offset, not version-keyed;
  `hsie.ini` gets a `ValN=` key name, no byte offset. **Version allowlist
  bar**: a version's offset is admitted per-site only after independently
  re-proving it against a real binary of that version — verify-before-write
  is a runtime safety net, not a substitute. **Stale-manifest guard**: the
  C# build re-runs the `tools/re-static/` generator and diffs it against the
  checked-in manifest, failing the build on drift. One ADR is still owed for
  this ticket's departure from ADR-0003 (fixed offsets + authoring-time proof
  here vs. runtime signature scanning, which stays right for `ape.act`'s own
  live anchors) — tracked in **Not yet specified**, not resolved here.
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

- **The diesel surfaced-gate's true identity — accepted as a named risk, not
  resolved.** Ticket 11 closed by scope pivot rather than RE proof: whether
  `[0x5543DC+0x18]`/`+0x1C` at `0x6350CA` genuinely means "surfaced" (vs.
  always-pass/dead) is still unknown. The 2nd live session found both fields
  reading a frozen, identical raw value across a surface→periscope-depth
  dive — suggestive of a dead gate, not confirmed (binary hash unchecked that
  session). Not a ticket, because there's nothing left to specify — the
  question is exactly ticket 11's, already fully written up there. Revisit
  from ticket 11's history if [ticket 12](issues/12-diesel-levers-blackbox-test.md)'s
  black-box test or later play surfaces evidence of submerged wear (and
  especially if that happens while provably not snorkeling).
  **Status 2026-08-16 — largely retired by black-box play, without ever
  identifying the fields.** Ticket 12's follow-up ran four conditions. The
  decisive pair is **surfaced + all stop (damage) vs submerged + stationary (no
  damage)**: under the exponent-0 rig the probability term was *identical* in
  both, so only the gate at `0x6350CA` can have suppressed the second. Therefore
  the gate is **not dead and not always-pass** — the reading the 2nd live
  session's frozen field values had suggested, from a session run against an
  unverified binary hash. Submerged on electrics: **no damage**, which is
  precisely the risk this entry was accepting, so **that risk is retired by
  observation.** Submerged on diesels (snorkel): damage — so **snorkelling
  counts as diesel running time**, answering ticket 11's explicit sub-question.
  ⚠ What remains genuinely open is only the **identity** of `[+0x18]`/`[+0x1C]`,
  which nothing now depends on, plus the assumption that case 4 was snorkelling
  (the only way SH3 runs diesels below the surface — worth confirming, since a
  no-snorkel diesel run flips that case from confirming to contradicting).
  Stays fog, not a ticket, and ticket 11 stays closed.
- ~~**Is a diesel at exactly `1.0` repairable at sea?**~~ **Answered 2026-08-16
  by ticket 12: no.** Crew assignment does not move the damage float off `1.0`;
  destruction is terminal for the patrol. Whether **base repair** clears it is
  untested — SH3's convention says yes, but nothing here distinguishes "dead
  until port" from "dead forever". Kept as one line because it feeds the open
  tuning question below, not because the mechanic is still unknown.
- ~~**Should APE's damage band be capped so two hits cannot destroy?**~~
  **Graduated 2026-08-16 into `.scratch/gui-patcher/`** by ticket 07 — it is
  tuning work for the v1 diesel feature's own scope, alongside the hull-repair
  default range, and no longer belongs to this map. Original note kept below for
  the numbers. Surfaced by
  ticket 12's repair result. Ticket 04 chose `0.15…0.55` knowing two unrepaired
  hits reach `1.0` about 3.1% of the time; that tail is now known to cost an
  engine for the rest of the patrol rather than merely hurt. Capping `hi` below
  `0.5` makes two hits mathematically unable to sum to `1.0` — but only defers
  it, since three hits still can. Not a ticket: it is a tuning judgement that
  belongs to the diesel feature's own scope work, and the number is a
  user-facing GUI field regardless of the default chosen.
- **Does `sh3.exe`'s `LoadLibrary` call site for `ape.act` tolerate a missing
  file?** Surfaced 2026-08-16 by ticket 06: reverting APE's last runtime
  feature now deletes `ape.act` from the game root, which only works if that
  call site is fire-and-forget like h.sie's/NSAP's own (ADR-0002 assumes this
  by design but nobody has disassembled *this* specific site to confirm it).
  Not a ticket yet because implementing the `sh3.exe` patch for that call site
  is itself still unticketed execution work — this rides along with whichever
  effort builds it, not the current frontier.
  **Status 2026-08-16 — deferred out of v1 with the rest of the `ape.act` path.**
  Ticket 07 ships no runtime feature, so nothing loads `ape.act` and the call
  site is not patched at all in v1. This question returns with the first version
  that ships an `ape.act` feature — which in practice means it is blocked behind
  crush-depth issue 01's dynamic CE hunts, not behind anything on this map.
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
  the C#/C++ toolchain split, and **[ticket 08](issues/08-signature-manifest-format.md)'s
  departure from ADR-0003** (fixed offsets + authoring-time signature proof for
  the patcher, vs. ADR-0003's runtime signature scanning, which remains right for
  `ape.act`'s own live anchors). This is execution work following settled
  decisions, not a decision itself; likely folded into whichever effort starts
  building the patcher — **as of 2026-08-16 that effort has a name:
  `.scratch/gui-patcher/`** (ticket 07). Note `src/patcher/main.cpp` is
  superseded rather than ported: v1's tool is the C# one.
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
  **Sidestepped for v1, 2026-08-16 by ticket 07:** the allowlist admits **only
  OAE 1.5.2 / `v 1.6E1`** — the build every site was derived on and ticket 12 was
  played on — and every other build is refused outright. So v1 does not need the
  play-target answered. It stays fog because adding `v 1.6B1` later still means
  re-proving every site against a real 1.5.3.7 binary at ticket 08's bar, and
  `4B6BEE20` remains unaccounted for.
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
