# Which compartment seeps on U-boats with no stern torpedo room (II, XXI)?

Type: grilling
Status: resolved
Blocked by: none

## Parent

.scratch/depth-pressure-stress/map.md. Raised by
[SH3's flooding model](10-sh3-flooding-model.md)'s answer.

## Question

Astern seepage fills one compartment. For VII and IX that is compartment 8, the stern
torpedo room. The Type II classes (2A, 2D) and the XXI have no compartment 8 (OAE's
`Basic.cfg`, `[SUBMARINE0]` and `[SUBMARINE3]`). Which compartment seeps on them?

- **Electric engine room (compartment 4).** On a live OAE IIA, F7 draws it sternmost,
  ahead of stern quarters, diesel and the rest (`docs/re-notes/08-oae-port-of-depth-leads.md`
  §7.1). It holds the motors and the shaft glands, which matches the historian's
  "stuffing boxes". Side effect: its water also cuts electric power, through
  `clamp(2·(flood − 0.2), 0, 1)`, so seepage there hurts submerged speed directly.
- **Stern quarters (compartment 7).** The vanilla lead's reading for the II ("II → 7").
- **Or by class:** whatever is sternmost on each class's damage screen.

Also say how `ape.act` picks it: a fixed table per class family, or a rule it applies
at run time (for example, the stern room if present, else the electric room).

## Answer (grilling with the owner, 2026-11-25)

- **The rule: the stern torpedo room (compartment 8) if the boat has one, otherwise the
  electric engine room (compartment 4).** For VII and IX that is compartment 8, as
  [ticket 10](10-sh3-flooding-model.md) found. For II and XXI it is compartment 4.
- **Why compartment 4.** On a live OAE IIA, F7 draws it sternmost
  (`docs/re-notes/08-oae-port-of-depth-leads.md` §7.1), and the motor room is the aftmost
  space on the real Type II and XXI. It holds the motors and the shaft glands the
  historian names. Compartment 7 ("SternQuarters") was rejected. In OAE's `Basic.cfg` it
  carries the II's stern planes, rudders and propellers, but F7 doesn't draw it astern.
- **The XXI is accepted from data, not seen live.** Its `Basic.cfg` compartment set
  (`[SUBMARINE3]`) is the same as the II's (`[SUBMARINE0]`): 0–7 and 10, no 8.
- **`ape.act` picks it at run time and never asks the boat's class.** It walks the zone
  list (head `0x005547D4`) and looks each zone up in the zone→compartment table
  `0x00566898`. If some zone maps to compartment 8, it seeps there; otherwise it uses the
  zone of compartment 4. No class identifier has been located, and the rule also covers
  U-boats that mods add. "Sternmost compartment" in `CONTEXT.md` stays true.
- **Accepted side effect: on II and XXI, seepage costs electric power.** Compartment 4's
  water cuts power through `clamp(2·(flood − 0.2), 0, 1)`: it starts at 0.2 flood and is
  total at 0.7, below the fixed 0.8 seep ceiling. An unpumped II or XXI at depth loses its
  motors; a VII or IX doesn't. The ceilings stay fixed and per class nothing changes:
  the crew pumps (~0.0033/game-s in a crewed compartment), and a short-range coastal
  boat in trouble at depth is the intended drama.
