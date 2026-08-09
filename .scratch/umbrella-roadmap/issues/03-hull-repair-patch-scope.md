# Hull-repair patch: scope

Type: grilling
Status: open

## Blocked by

02

## Question

Goal 2: patch the hsie/stiebler u-tanker mod's hull-repair behavior so hull is
no longer repaired, while every other function of that mod keeps working.
Stretch goal: make the amount of hull repair settable via `ape.cfg`, using
the config strategy decided in ticket 02 (shared ape.cfg strategy across
subsystems).

Pin down enough to spin up its own `.scratch/hull-repair-patch/` feature
effort (PRD + issues, same shape as `crush-depth-damage`):

- Exact behavior when disabled: hull repair amount forced to zero, the
  repair function no-op'd/hooked out entirely, or something else? What
  "every other function keeps working" excludes (list the u-tanker mod's
  other functions so the boundary is explicit).
- Whether this is a config-off-by-default toggle or hull-repair is always
  disabled with the stretch goal only adjusting the *amount* (i.e. is "off"
  itself ever configurable, or only the repair rate when not fully off).
- Whether locating the relevant function is RE work in the same style as
  crush-depth-damage's AOB-anchor hunt (`.scratch/crush-depth-damage/issues/01-*`)
  — if so, note it as the first issue of the new feature effort rather than
  resolving it here.
