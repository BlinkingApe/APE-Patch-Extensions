# ape.act hook installation, live wiring, and debug logging

Status: ready-for-agent

## Parent

.scratch/crush-depth-damage/PRD.md

## What to build

The core end-to-end slice: `ape.act`'s `DllMain` AOB-scans for the real crush-depth damage function (using the signature from 01-re-crush-depth-damage-signature), installs a MinHook inline hook on it, reads live depth and crush-depth from game memory at the hook site, calls the damage-band decision function (03) with config loaded via (04), applies the resulting damage rate (or calls through to the original/stock behavior when past the heavy band), and logs depth, percent-under-crush-depth, selected band, and applied rate on every tick the hook runs.

## Acceptance criteria

- [ ] `ape.act` builds as a PE32 DLL via CMake/MSVC with MinHook vendored.
- [ ] On load, it successfully resolves the crush-depth damage function via AOB scan against the real OAE sh3.exe.
- [ ] The MinHook inline hook installs successfully and the original function pointer is preserved.
- [ ] At runtime, live depth and crush-depth values are read from game memory at the hook site.
- [ ] The damage-band decision function is called with correct inputs, and its result (band + rate, or "defer to stock") is applied correctly — including calling through to original behavior when deferring.
- [ ] Every tick the hook runs, a debug log line is written recording depth, percent-under-crush-depth, selected band, and applied damage rate.
- [ ] Verified via the debug-log + Cheat Engine-forced-depth loop: forcing the submarine to each band's test depths produces log lines matching expected arithmetic.
- [ ] For development, `ape.act` may be loaded into the running game process via direct debugger injection (x32dbg) rather than the production `sh3.exe` load-call patch — this ticket does not require 02-re-sh3exe-load-call-site or the patcher tool to be complete.

## Blocked by

- 01-re-crush-depth-damage-signature (needs the real function signature)
- 03-damage-band-decision-function
- 04-ape-cfg-config-loading
