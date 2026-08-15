# OAE sh3.exe build registry

Each APE hook target / patch site is verified against a specific OAE sh3.exe
build, identified by the SHA-256 of the exact binary (version labels are
informal — the hash is the source of truth). Add a row per build we work
against; reference builds from the per-target notes by their short hash.

| Short hash | SHA-256 (full) | Size (bytes) | Human label / date | Notes |
|------------|----------------|--------------|--------------------|-------|
| `4B6BEE20` | `4B6BEE20AD9843474D72944EE4811EBB055A8FC4A3377FB692E75731307F347B` | 1,417,216 | current install / 2026-07-13 | hsie params: `Hit_Points=500.0`, `CrashSpeed=7.0` damage/s |

## h.sie version survey (2026-08-15)

Four installs on the dev machine, surveyed while resolving umbrella-roadmap
ticket 05. **The h.sie version string is at VA `0x63CC00` / file `0x14EC00`**;
`HSIEContents.txt` states it is intended as a version test. The flag table
(`0x14EF00`) and slider array (`0x14E074`) are at the **same offsets in both
versions** — the two installs' `HSIEContents.txt` files differ by exactly one
line, the version string.

| Install | h.sie ver | Size | sha256 | Diesel Damage flag |
|---|---|---|---|---|
| `C:\games\OneAlex Extended 1.5.2` | `v 1.6E1` | 1,417,216 | `51d14ca3…` | **off** |
| `C:\games\OneAlex_1_5_3_7` | `v 1.6B1` | 1,384,448 | `1b6a5823…` | **off** |
| `C:\games\KC2.5` | `v 1.6B1` | 1,384,448 | `a7b5fc03…` | **off** |
| `C:\games\GWX 3.0 Gold` | `v 1.6E1` | 1,417,216 | `79a5321d…` | **off** (all 22 off) |

**The newer OAE ships the older h.sie.** A version allowlist is therefore needed
to support OAE alone, not merely to be inclusive of KC/GWX.

⚠ **Diesel Damage is disabled in every install surveyed.**

⚠ **The "GWX 3.0 Gold" exe is byte-identical to OAE 1.5.2's** across stock `.text`
and h.sie `.pcode` (0 differing bytes in 1.36 MB); it differs *only* in the flag
table, where everything is `0x00`. That folder almost certainly holds an
OAE-derived binary, not a genuine GWX one — **do not count it as an independent
data point** until confirmed.

### Cross-build stability (what licenses fixed offsets)

Divergence measured against OAE 1.5.2:

| Region | OAE 1.5.3.7 | KC2.5 | GWX 3.0 |
|---|---|---|---|
| stock `.text` (`0x1000`–`0x145000`) | 0.01% | 0.19% | 0.00% |
| h.sie `.pcode` (`0x145000`–`0x14E000`) | 1.16% | 2.87% | 0.00% |

The **hull-repair cave** (`VA 0x633633` / file `0x145633`) is present at the
**identical offset and matches exactly once** in all four binaries, across both
h.sie versions. This is the evidence that licensed dropping runtime AOB scanning
in favour of version-keyed fixed offsets (ticket 05, amended).

The 1–3% `.pcode` divergence between versions is the reason
**verify-expected-old-bytes before writing is retained** — most caves hold still
across versions, but not provably all of them.

<!--
To fill the current row, from the install folder:
  PowerShell:  Get-FileHash .\sh3.exe -Algorithm SHA256
               (Get-Item .\sh3.exe).Length
Record the full hash, its first 8 hex chars as the short hash, the byte size,
and whatever OAE version/date label you use to refer to this build.
-->
