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

**Status:** resolved

- [x] `ape.act` still builds as PE32/x86 and contains the marked, versioned struct with the ticket-12 defaults
- [x] The build records the struct's file offset in the generated manifest, and the manifest's stale-check guard still passes
- [x] On a matching marker, startup logs the constants and starts exactly one loop thread; nothing else happens under `DllMain`
- [x] On a corrupted marker or version, the log says so and no loop thread starts (fail inert)
- [x] The ceilings (seep 0.8, wear 0.9) are fixed constants in `ape.act`, not fields of the struct
- [x] A doctest target builds alongside `ape.act` and its placeholder test runs green

## Comments

**2026-11-25, implemented.**

- Struct: `src/mods/depth_stress/constants.hpp`. Marker `APEDPTH1`, then a u32 version 1, then
  nine float32 fields in **runtime units**: the patcher converts (`p/6000`, `1/(60·m)`,
  `a/100`), as ticket 22 says. 48 bytes, with every offset pinned by `static_assert`. The
  ceilings and the 100 m tier-1 depth are `constexpr`, not fields. The one instance is
  `src/ape_act/depth_stress_constants.cpp`, read through a volatile byte copy so a patched
  value is never folded away.
- Loader: `DllMain` copies the struct, verifies it (a memory compare) and starts one thread.
  The thread does the logging. On a match it logs the constants and polls at 100 ms, doing
  nothing yet. On a mismatch it logs the reason and returns without entering the loop, so no
  file I/O happens under the loader lock.
- Manifest: a Release build's POST_BUILD runs `gen_manifest.py --ape-act <ape.act>`. That
  checks the file is PE32/x86, finds marker+version exactly once inside `.data`, and records
  `apeAct.structs[0]` (offset, size, magic, version, field `at`s). The bare marker also
  appears in `.rdata` from verify()'s compare, which is why the search includes the version
  and the section. The template is `build/x86-release/ape.act`, struct at 0x6008. When that
  isn't built, `--check` reuses only the recorded offset and rebuilds the rest of the block.
  The C# build therefore still works without a C++ build, but the offset is then unmeasured.
  Ticket 22, which embeds the template, should make the binary mandatory.
- Tests: `tests/depth_stress_test.cpp` (`ape_tests`, ctest) covers the defaults, verdicts
  for a good, corrupted-marker and wrong-version struct, and the placeholder step (no sample
  means no writes). Both presets are green.
- Checked by hand with a throwaway x86 host that LoadLibrary'd copies of the Release build.
  Good copy: one ape.act thread alive and the constants logged. Corrupted marker or version:
  zero ape.act threads alive and "constants rejected (…): loop not started". A tier-2 depth
  patched to 200 m at the manifest offset was logged as 200 m.
