# Diesel-damage patch: scope

Type: grilling
Status: open

## Blocked by

02

## Question

Goal 3: patch the hsie diesel-damage mod so there's a chance of damage after
eight hours (of what — running time? mission time? needs pinning down).
Stretch goal: make damage amount, damage chance, and time-until-damage-chance
all settable via `ape.cfg`, using the config strategy decided in ticket 02
(shared ape.cfg strategy across subsystems).

Pin down enough to spin up its own `.scratch/diesel-damage-patch/` feature
effort (PRD + issues, same shape as `crush-depth-damage`):

- What "eight hours" measures (engine running time, mission clock, real
  time) and what timer/counter the existing hsie mod already exposes for
  this, if any.
- Damage-chance model: rolled once at the eight-hour mark, or a recurring
  chance-per-interval after eight hours? Affects both the base behavior and
  what "chance" means as a config key.
- Damage amount semantics: flat HP hit, rate, or band (cf. crush-depth
  damage's HP-per-game-second bands) — does this reuse `src/core/damage_band.*`
  or need its own model?
- Whether locating the relevant function/timer is RE work in the same style
  as crush-depth-damage's AOB-anchor hunt — if so, note it as the first issue
  of the new feature effort rather than resolving it here.
