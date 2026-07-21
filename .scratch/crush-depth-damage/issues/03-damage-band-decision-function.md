# Damage-band decision function + unit tests

Status: ready-for-agent (reopened — pivot rework, see Comments)

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

The pure damage-band decision function: given current depth and crush depth (its
ratio, `under = (depth − CDT)/CDT` clamped at 0, is what matters), plus the
light/medium/heavy band configuration (two cumulative window boundaries as
fractions of crush depth + three flat rates), return which band applies and the
**HP-per-game-second** rate to use. The heavy band is **open-ended**: past the
medium boundary, heavy always applies — there is no "defer to stock" / past-heavy
signal (APE owns 100% of crush-depth damage; PRD). No I/O, no game-memory access.
The caller multiplies the returned rate by the game-time delta `dt` and subtracts
from HP. This is the one automated-test seam agreed for this hack.

## Acceptance criteria

- [ ] The function correctly selects the light, medium, or heavy band for depths
      within each respective window (`light [0,a)`, `medium [a,b)`, `heavy [b,∞)`).
- [ ] Boundary depths (exactly at a window edge) resolve to the correct adjoining
      band, consistently.
- [ ] Depth far below crush depth resolves to the open-ended **heavy** band and
      returns the heavy rate — it never signals "defer" or fabricates a rate.
- [ ] Depth at or above crush depth (zero percent under) resolves to the light
      band.
- [ ] A degenerate config (a band with zero width) is handled without crashing or
      misclassifying (the zero-width band is simply never selected).
- [ ] The returned rate is documented and tested as HP-per-game-second (the caller,
      not this function, applies `dt`).
- [ ] Unit tests (doctest, building under CMake/MSVC) cover all of the above and
      pass.

## Blocked by

None — pure logic, no RE dependency, no game interaction.

## Comments

**COMPLETE (Windows 11, commit `1acf9be`) — now partially STALE.** Implemented as
`ape::select_band` in `src/core/damage_band.{hpp,cpp}` (`ape_core` lib), tests in
`tests/damage_band_test.cpp`, doctest vendored under `third_party/doctest/`.

**REOPENED (2026-07-21, architecture pivot).** The pivot to "APE owns 100%,
alongside" changes this function's contract. Required rework:
- **Remove `Band::PastHeavy`, `BandResult::defer_to_stock`, and `heavy_window`
  from `BandConfig`.** The model is now three bands with two boundaries; heavy is
  everything past the medium edge (open-ended). Config carries `light_window`,
  `medium_window`, and three rates only.
- **Rate units are HP-per-game-second** (were "damage-per-tick"). The function's
  contract/comments must say so; the loop caller multiplies by `dt = Δ(mission
  clock)`.
- **Update the tests:** replace the "past heavy → defer_to_stock" case with a
  "far below crush depth → open-ended heavy rate" case; keep the binary-exact
  boundary fractions (1/8, 1/4, 1/2) so edge tests stay deterministic.
- Coordinate with issue 04: dropping `heavy_window` removes the
  `heavy_damage_window` config key.
