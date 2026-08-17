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
  crush-depth damage, untouched. **Crush depth is never read**: APE's effects simply
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

## Decisions so far

<!-- charted 2026-08-16; the model above was settled in the charting session's
     grilling and is recorded in Notes, not here -->

- [Retire the superseded crush-depth model's artifacts](issues/13-retire-superseded-model.md) — dead tickets deleted, issue `01` and `05` amended, `CONTEXT.md` glossary reversed to `depth tier`, ADR-0008 written for the model reversal + poll-and-amplify limitation. Surfaced ticket `15` for the dead C++ still linked into the build.

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

- **Owning the crush-depth threshold or the damage past it.** The superseded model
  had APE own 100% of below-crush damage with stock's `CrashSpeed` zeroed. The new
  model hands tier 3 back to stock entirely and never reads CDT. Re-opening this
  would be redrawing the destination, not advancing toward it.
- **Amplifying anything but hull HP.** Compartment damage, equipment damage and
  flooding caused by depth charges stay at stock magnitude. APE's own seep and wear
  effects cover that ground instead. Scaling everything a hit produces would mean
  mapping every damage float the engine writes — open-ended RE with no known anchor.
- **JSGME packaging and per-unit data edits.** Ruled out by the umbrella map
  (`.scratch/umbrella-roadmap/map.md`, ticket 05) and now doubly dead: with
  `CrashSpeed` untouched there is no data edit left to ship. Old issue `07` is
  deleted by ticket 13, not resolved.
