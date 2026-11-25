# Depth-pressure stress: hull wear, seepage, and system wear from depth

Label: wayfinder:map

## Destination

A **rewritten `.scratch/depth-pressure-stress/PRD.md`** describing the new model —
absolute-depth tiers applying a hull-damage multiplier, continuous astern seepage,
and probabilistic electric-engine wear — in which **every mechanic has a located,
proven write target**. Reaching the end means the five live values are resolved and
documented, the dead artifacts of the superseded model are gone, and nothing is left
to decide before implementation issues can be spun up.

RE-informed, deliberately: the destination is *not* a paper spec. A mechanic whose
write target is unlocated is not specified.

## Notes

- **Effort renamed 2026-08-17**: `.scratch/crush-depth-damage/` → `.scratch/depth-pressure-stress/`,
  reflecting that the destination now covers hull wear, astern seepage, *and*
  system (electric-engine) wear from depth — not crush-depth bleed alone. Ticket
  filenames and historical references to "crush-depth-damage" (old tickets `01`–`07`,
  `docs/re-notes/01-crush-depth-damage-signature.md`) are left as-is; they're stable
  identifiers for work already done, not live effort names.
- Domain: OAE sh3.exe / `.act` DLL modding — see `CONTEXT.md` at repo root.
- **This map supersedes the model in the existing `PRD.md`**, which describes three
  fraction-of-crush-depth bands of flat HP-per-second hull bleed with stock's
  `CrashSpeed` neutralized. That model is abandoned whole. The PRD is rewritten
  **in place** when this map resolves; issues `03` (band decision function), `04`
  (`ape.cfg`), `06` (C++ patcher tool) and `07` (JSGME packaging) are dead and are
  deleted by ticket 13. Wayfinder tickets are numbered from `08` to avoid colliding
  with the surviving old issues until then.
- **The new model in one paragraph.** Below a configured absolute depth (default
  ~100m) tier 1 applies; below a second configured depth, tier 2 — same effects,
  more so. Each tier carries a flat hull-damage **multiplier**, a flat **astern
  seep rate**, and a flat **electric-engine wear chance**. Tier 3 is stock's own
  crush-depth damage, untouched. *(Revised 2026-11-25, ADR-0010: tier 2 now ends at
  crush depth, read live; APE sets stock's crush speed; see the grilling note below.)*
  **Crush depth is never read**: APE's effects simply
  continue below it rather than switching off.
- **The historian's premise** (via the user, 2026-08-16): "All but the late war
  variants of the boats, and even the type II, were designed for an operating depth
  of 100 m. With a 2.5 safety margin, so calculated crush 250, this was standard
  German design practice. So shallower than 100 m, there shouldn't be any problems.
  But below this depth a tiered approach makes sense. The hull is under increasing
  stress the deeper you go." On systems: "Cooling water system needed to be secured,
  those lines were only pressure tight down to design depth. So bearings running
  hot, thrust bearings, motor bearings."
- **Alongside, never hooking, still holds.** The multiplier is therefore
  **poll-and-amplify**, not interception: APE notices hull HP fell by N since last
  tick and subtracts a further `(mult−1)·N`. It cannot attribute the loss to a
  cause, and does not try to.
- **Repairability is a proven constraint, not a preference.** Ticket 12 on the
  umbrella map established in play that a system at exactly `1.0` cannot be repaired
  at sea. Depth wear is always-on and therefore accumulates across a long transit,
  so it is clamped by a **hard ceiling APE never writes past** — the guarantee is by
  construction, not by tuning the odds. See that ticket for why statistical
  guarantees were rejected: APE's diesel band still reached `1.0` 3.1% of the time.
- **The critical path is five dynamic RE hunts**, all needing a human at the
  controls with Cheat Engine. This is exactly what has blocked old issue `01` since
  2026-07-21. Two of the five (stern compartment water, electric-engine damage
  float) have **no anchor and no offset** — the diesel float at
  `[0x5543DC+4]+0x30C` is the only precedent for an equipment-damage value, and it
  was found by accident of h.sie's cave, not by walking the equipment table.
- **Hash the running exe before any live CE session.** Umbrella ticket 11 burned a
  session on addresses that never resolved against an unverified binary; ticket 12's
  hash-first discipline had every address resolve first try. See
  `docs/re-notes/01-live-session-playbook.md`.
- **Read the install, not just the repo** — `C:\games\OneAlex Extended 1.5.2` and
  `data/Menu/en_menu.txt` settled more umbrella questions than disassembly did.
- Use `/grilling` and `/domain-modeling` on this map; `/to-spec` to write the PRD
  once the way is clear.
- **Static pass 2026-08-17** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §6): a candidate static root (`0x5543DC`) for the player-boat/movementObj chain,
  plus an offset survey of a large shared object it reaches that narrows where the
  electric-engine damage float likely lives. Not CE-confirmed — sharpens tickets
  `08`, `09`, `11` but doesn't resolve them; still HITL work. No static candidate
  found for the mission clock or the flooding trigger (ticket `10`).
- **Static pass 2026-08-17, second round** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §7): re-audited the map for stale cross-references rather than scanning fresh,
  and found that ticket `09`'s exact target — a working pointer chain to the player
  boat, for the hull-HP write — was **already sitting in this repo**,
  undiscovered-by-name in `03-utanker-hull-repair-signature.md`'s hull-repair cave
  write-up. `[0x554A88]→+0xC4→+0x2240→+0x24→+0x4C]` = boat, proven by shipped code
  that already reads/writes `[boat+0xB4]`/`[boat+0xB8]` through it, AOB-anchored
  and re-verified against the current install. Does not resolve `09` outright —
  the write-sticks-under-`ape.act`'s-own-writer and depth-charge-delta criteria
  are still HITL — but narrows its live session from a pointer-scan to a
  confirmation. No equivalent shortcut found for `08`, `10`, or `11`.
- **Static pass 2026-08-17, third round** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §8): chased whether ticket `09`'s `0x554A88` boat chain also reaches
  `movementObj` for ticket `08`. It doesn't — checked exhaustively, both
  directions, zero hits. Further found the depth-setter's caller has **no static
  root reachable at all** (zero direct callers, only an untraceable vtable slot) —
  closes ticket `08`'s depth half definitively to a from-scratch CE scan, not a
  narrowing. Also ruled out the one mission-clock candidate static analysis had
  produced (§6.3's `+0x358`/`+0x360` doubles — a wear-ratio accumulator, not a
  clock) and reassigned it as a possible lead for ticket `11` instead. Net: `08`
  is now a plain live-value hunt with no shortcut left to look for; `11` gained a
  cheap thing to check first.
- **Static pass 2026-08-17, fourth round** (`docs/re-notes/06-flooding-model-
  signature.md`, new note): ticket `10` started from zero — no anchor, no
  precedent — so this pass went strings-first instead of tracing a known field.
  Found SH3's own interior-camera data tags its sternmost compartment "DIESEL
  ENGINE ROOM;InteriorAft" (evidence, not proof, for which compartment is
  "astern"), and one unconfirmed flooding read/compare (`[ecx+0x1C]` vs `0.35` at
  `0x436BE4`, `ecx`'s identity unresolved) as a lead for a live session to watch.
  h.sie's caves have no flooding/pump precedent to borrow, unlike hull repair or
  diesel damage. Two leads handed off; the mechanism decision, value anchor, and
  compartment identity all remain fully HITL.
- **Static pass 2026-08-17, fifth round** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §9): chased ticket `11` (electric-engine float) directly. Found most of §6.3's
  offset survey was mis-attributed by a register-reuse scan artifact — corrected
  the table. Ruled out both remaining candidates (`+0x438`/`+0x440`: a
  torpedo-reload flag, not a damage float; string IDs 462/463: zero occurrences
  anywhere in the binary, unlike diesel/flooding/torpedo). `+0x358`/`+0x360`
  (fourth-round pass, above) remains the only lead, neither promoted nor demoted.
  Ticket `11` is otherwise unchanged — a from-scratch live scan, now with a
  cleaner map of what's already been ruled out.
- **Live pass 2026-08-17** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §10): first live CE session against ticket `08`. Mission clock is **fully
  resolved** — static address `0x0063FF80`, 4-byte integer, no chain needed,
  confirmed pause/time-compression behavior and relaunch stability. The
  `movementObj` chain-walk is started but not finished: the base register (ECX at
  `00514001`) is confirmed, but the pointer-scan to a static-rooted chain wasn't
  run — the session ended after CE/the game crashed when a breakpoint was left
  held open too long on a hot per-frame path. Ticket `08` remains open, claimed,
  with one criterion (mission clock) done and the other (movementObj chain) ready
  to resume from a known register.
- **Live pass 2026-08-17, second session** (`docs/re-notes/01-crush-depth-damage-signature.md`
  §11): ticket `08` fully resolved. Disambiguated three live-tracking depth
  candidates via "find what writes," confirming `movementObj+0x54` = `0x3BA70784`
  and `movementObj = 0x3BA70730` for that instance. CE pointer scan (level 6,
  restricted to `sh3.exe`'s module range) plus a two-relaunch pointermap
  comparison narrowed 84 candidates to 3, the shortest of which —
  `[[["sh3.exe"+002307BC]+4]+4]+0x54` — ends in the already-known `0x54` field
  offset, confirming it's real rather than incidental. Resolved and live-tracking
  confirmed against the depth gauge; stable across two independent relaunches.

- **External RE pass 2026-11-25, sh3-crush-pressure (vanilla Steam 1.4b;
  portability: owner).** A separate repo (`d:\code\sh3-crush-pressure`, branch
  `automated-depth-re` until merged) ran tickets `10`, `11` and `12`'s questions
  unattended under Frida on the unwrapped **vanilla Steam SH3 1.4b** exe (sha256
  `1f14568235f903e7e50b73d3caa937b6946aadf7013e3dc39dfd1cc7975986f2`), not OAE.
  Each of those tickets carries a lead section summarising it; **none is
  resolved** — porting to OAE and resolving are the owner's call. Also two things
  for this map:
  - **Hull-HP writer inventory** (`docs/re-notes/hull-writer-inventory.md`,
    `docs/findings/hull-hp-writers.json`; 15 runs over crush, ramming, grounding,
    depth charges, mission start and save-load, hardware watchpoints on hull and
    max hull). Damage writers are **two**, not one: `SH3Collisions+0x30D7` (crush,
    every frame, cause 4 — ticket `09`'s `02C930D7`) and `+0x36D7` (ramming and
    depth charges cause 1, grounding cause 3, single steps). Load/init:
    `+0x4E3C`/`+0x4E48` (copy constructor), `+0x2919` (hull := max). **No repair
    writer: hull never rose at sea.** The `0x566A28` mirror (fog entry below) is
    the HUD's copy, refreshed every ~10 s by `0x004E1C9F`: no use for the read half.
  - **One hazard for poll-and-amplify: the save-load restore.** A byte-wise copy
    (`SH3Collisions+0x2376…+0x2383`, via `MessageNet.dll` from `Multiplayer.act`)
    lowers hull from max to the saved value ~0.8 s after the new object appears,
    with no damage cause. Unguarded, every load of a damaged boat would cost a
    further `(mult − 1) × (max − saved)` HP. **Safety argument:** poll-and-amplify
    amplifies only damage, and never fights a repair, with two guards — (1)
    re-baseline when the chain reaches a different object; (2) don't amplify a drop
    if the mission clock read 0 at the *previous* poll (the restore lands at clock
    0; the clock jumps to the saved time ~0.1 s later). Guard 2 rests on one
    clock-polled load, and on the stock clock `[["sh3.exe"+0x154A88]+0x7F0]+0x150`;
    **whether h.sie's `0x0063FF80` mirror also reads 0 through the restore is for
    the owner to check on OAE.** Fallback: hold the baseline ~2 s after an object
    change. Depth-charge damage mostly bypasses hull (one boat sank "destroyed by
    flooding" at full hull), so the multiplier can't scale it — a design point for
    ticket `12`, not a safety one.

- **Grilling 2026-11-25: player fields, tier 3, and a crush-time patch** (ADR-0010,
  `CONTEXT.md`, the field table in ticket `12`). Tier 1: 100 m (fixed) to a player-set
  tier-2 depth. Tier 2: to crush depth, which APE now reads live (ticket `17`). Tier 3:
  stock's crush damage at a speed APE sets. Seepage, wear and the multiplier continue
  below crush depth at tier-2 values, except that crush damage is never multiplied
  (`crush speed × dt` of each drop is exempt). Per tier the player sets leakage, wear
  amount, wear rate and hull multiplier. The ceilings (0.8 seep, 0.9 wear) stay fixed.
  A separate **crush-time patch** (ticket `16`, blocked by `18`'s U-boat discovery rule)
  writes `CrashSpeed = Hit_Points ÷ crush time` into each playable U-boat's `.zon`. It is
  static and can ship before `ape.act`. Basis: the owner's in-game testing that
  `CrashSpeed` is HP per second, which supersedes the external RE's
  `excess × CrashSpeed × dt` reading. Tier-2 depth defaults to 150 m. Crush time defaults to 5 s for every U-boat (set after ticket `18`).

## Decisions so far

<!-- charted 2026-08-16; the model above was settled in the charting session's
     grilling and is recorded in Notes, not here -->

- [Retire the superseded crush-depth model's artifacts](issues/13-retire-superseded-model.md) — dead tickets deleted, issue `01` and `05` amended, `CONTEXT.md` glossary reversed to `depth tier`, ADR-0008 written for the model reversal + poll-and-amplify limitation. Surfaced ticket `15` for the dead C++ still linked into the build.
- [Retire the superseded band-decision model's C++ implementation and build wiring](issues/15-retire-superseded-model-source.md) — deleted the whole `src/mods/crush_depth_damage/` tree, `ape.cfg`, and `config_check` outright rather than keeping a reusable shell; `dllmain.cpp` reduced to a minimal stub. Build verified clean (PE32/x86), no live reference to the dead model remains outside historical prose.
- [Live-value session A: current depth + mission clock](issues/08-live-values-depth-and-clock.md) — both resolved: mission clock is static VA `0x0063FF80`, no chain; current depth is `[[["sh3.exe"+002307BC]+4]+4]+0x54`, a 3-level static chain confirmed stable across two relaunches.
- [Live-value session B: boat pointer chain for the hull-HP write](issues/09-live-values-hull-hp-write.md) — resolved: `[0x00554A88]→+0xC4→+0x2240→+0x24→+0x4C]+0xB8` confirmed live (write holds); found the engine's own universal hull-HP writer (`02C930D7`, per-frame, single `fsub` per collision, cause-agnostic) as a bonus, safe for poll-and-amplify by construction. Delta observed: `450 → 448.2609`.
- [Which `.zon` files are playable U-boats? A discovery rule that works for OAE and KC](issues/18-playable-uboat-zon-discovery.md) — `[Unit] HumanPlayable=YES` in `data/Submarine/<X>/<X>.cfg`; the same 8 classes in both installs (2A, 2D, 7b, 7c, 9b, 9c, 9d2, 21), XIV and other AI U-boats excluded; `.zon` key/float layout confirmed for all 42 files. It also showed that live OAE crush speeds come from the owner's JSGME test mod (stock VIIC ≈ 6250 s, KC 250 s). The owner then set the crush-time default to 5 s for every U-boat. APE replaces the test mod, so there's no JSGME conflict.

## Not yet specified

- **Which APE version ships this, and on what `ape.act` footing.** Every effect here
  is inherently runtime, so this feature requires `ape.act` — which the umbrella map
  deferred *whole* from patcher v1 (ticket 07), precisely because crush-depth was
  blocked on dynamic hunts. It also re-raises the umbrella's open question of whether
  `sh3.exe`'s `LoadLibrary` call site tolerates a missing file. Not a ticket here:
  that is the umbrella map's decision to make, not this one's, and it is unblocked by
  this map's resolutions rather than blocking them.
- **Is pumping while still deep a losing battle by design?** Continuous seepage means
  a crew pumping at 150m may never win until the boat ascends. That may be exactly
  the intended drama or may be miserable in play; it cannot be judged before the seep
  rate is a real number against real pump rates. Tuning, not mechanism — expected to
  be absorbed by ticket 12 or by play.
- **Whether depth wear should extend past the electric engines.** One system was
  chosen to keep the RE bill to one unmapped value, with expansion left open. The
  historian's list (cooling water, thrust bearings, motor bearings) plausibly reaches
  the propeller shafts and compressed air. Cannot be specified until ticket 11 reveals
  whether SH3's equipment-damage values are a walkable table or one-off floats.
- **Does the hull-HP static mirror at `0x566A28` help here?** It is always-current and
  read-only (writes are overwritten by the engine), found during the static pass. It
  may serve the multiplier's *read* half — and, if so, may reduce ticket 09 from a
  write-capable pointer chain to something cheaper. Too speculative to ticket until
  someone tries it live.
- **How the tier constants reach `ape.act`.** ADR-0006 already decides the mechanism
  (a magic-marked struct in `ape.act`'s own `.data`, written by the C# patcher at a
  build-recorded offset), so this is not an open decision — but the specific field
  list, ranges, and defaults for this feature's manifest sites are unwritten and
  follow ticket 12's numbers.

## Out of scope

- **Owning the crush-depth threshold or the damage past it.** *(Narrowed by ADR-0010: APE now sets stock's crush speed via `.zon`, but stock still owns the mechanism.)* The superseded model
  had APE own 100% of below-crush damage with stock's `CrashSpeed` zeroed. The new
  model hands tier 3 back to stock entirely and never reads CDT. Re-opening this
  would be redrawing the destination, not advancing toward it.
- **Amplifying anything but hull HP.** Compartment damage, equipment damage and
  flooding caused by depth charges stay at stock magnitude. APE's own seep and wear
  effects cover that ground instead. Scaling everything a hit produces would mean
  mapping every damage float the engine writes — open-ended RE with no known anchor.
- **JSGME packaging and per-unit data edits.** *(Per-unit edits revived by ADR-0010 for `CrashSpeed` only, through the patcher, not JSGME; ticket `16`.)* Ruled out by the umbrella map
  (`.scratch/umbrella-roadmap/map.md`, ticket 05) and now doubly dead: with
  `CrashSpeed` untouched there is no data edit left to ship. Old issue `07` is
  deleted by ticket 13, not resolved.
