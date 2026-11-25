# 21: `ape.act` constants struct, verified loader and test harness

**What to build:** the setup work every later depth-stress ticket builds on. `ape.act`
carries one magic-marked, versioned depth-stress constants struct (ADR-0006) in its own
`.data`, holding the PRD's ticket-12 defaults as float32: tier-2 depth, and per tier the
seep rate, wear amount, wear rate and hull multiplier. At startup the loader checks the
marker. On a match it logs the marker and constants to `ape_act.log` and starts one
background loop thread polling at about 10 Hz that does nothing yet. On a mismatch it
logs the reason and never starts the loop. It does no work under the loader lock and
hooks no function (ADR-0001). The build writes the struct's file offset into the
generated manifest so the patcher can find it. A vendored-doctest test target builds and
runs, with a placeholder depth-stress step function so later tickets have somewhere to
add tests.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `ape.act` still builds as PE32/x86 and contains the marked, versioned struct with the ticket-12 defaults
- [ ] The build records the struct's file offset in the generated manifest, and the manifest's stale-check guard still passes
- [ ] On a matching marker, startup logs the constants and starts exactly one loop thread; nothing else happens under `DllMain`
- [ ] On a corrupted marker or version, the log says so and no loop thread starts (fail inert)
- [ ] The ceilings (seep 0.8, wear 0.9) are fixed constants in `ape.act`, not fields of the struct
- [ ] A doctest target builds alongside `ape.act` and its placeholder test runs green
