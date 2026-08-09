# Shared ape.cfg strategy across subsystems

Type: grilling
Status: open

## Blocked by

None

## Question

`ape.cfg` today carries crush-depth-damage's band config only (`docs/adr/0004-config-format-and-location.md`,
`src/core/config.{hpp,cpp}`). Two of the new candidate goals have stretch
goals that make settings config-driven: hull-repair amount (item 2) and
diesel-damage chance/amount/timer (item 3). Decide:

- Does every subsystem get its own top-level section in one shared `ape.cfg`,
  or does each subsystem ship its own config file (loaded the same way)?
- Naming/namespacing convention for keys so sections don't collide as
  subsystem count grows (e.g. `[crush_depth]`, `[hull_repair]`, `[diesel_damage]`).
- Whether `src/core/config.{hpp,cpp}` stays one shared parser returning one
  struct (extended per subsystem) or moves to a per-subsystem parse step over
  a shared low-level reader.
- Backward compatibility: does the shipped `data/Cfg/ape.cfg` need a version
  marker or migration note once it grows multiple sections?

Answer should be concrete enough that hull-repair's and diesel-damage's
stretch-goal config keys can be named and typed without re-opening this
question.
