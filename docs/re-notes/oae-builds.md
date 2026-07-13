# OAE sh3.exe build registry

Each APE hook target / patch site is verified against a specific OAE sh3.exe
build, identified by the SHA-256 of the exact binary (version labels are
informal — the hash is the source of truth). Add a row per build we work
against; reference builds from the per-target notes by their short hash.

| Short hash | SHA-256 (full) | Size (bytes) | Human label / date | Notes |
|------------|----------------|--------------|--------------------|-------|
| `4B6BEE20` | `4B6BEE20AD9843474D72944EE4811EBB055A8FC4A3377FB692E75731307F347B` | 1,417,216 | current install / 2026-07-13 | hsie params: `Hit_Points=500.0`, `CrashSpeed=7.0` damage/s |

<!--
To fill the current row, from the install folder:
  PowerShell:  Get-FileHash .\sh3.exe -Algorithm SHA256
               (Get-Item .\sh3.exe).Length
Record the full hash, its first 8 hex chars as the short hash, the byte size,
and whatever OAE version/date label you use to refer to this build.
-->
