# Which compartment seeps on U-boats with no stern torpedo room (II, XXI)?

Type: grilling
Status: open
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
