# ape.act is loaded via a dedicated sh3.exe patch, not by proxying hsie.act

OAE sh3.exe already loads two custom `.act` modules, `hsie.act` and `sh5.act` (NSAP). Inspection of the actual install (`file`, `objdump`, and `strings` in both ASCII and UTF-16LE) showed both are ordinary PE32 DLLs with empty export tables that do all their work in `DllMain`, and that neither filename appears as a literal string anywhere in `sh3.exe` or `sh3.dll`. This means their loading isn't a generic "scan the folder for `*.act`" mechanism — each is a hand-patched `LoadLibrary` call site added directly to `sh3.exe`'s code.

Two ways to get `ape.act` loaded followed from this: proxy `hsie.act` (rename the original, forward to it via `DllMain` — since it has no exports, no export-forwarding is needed, and this requires zero further changes to `sh3.exe`), or patch `sh3.exe` with a new, dedicated `LoadLibrary` call site for `ape.act`.

Chose the dedicated patch. This means a second artifact (the new call site) needs re-verifying whenever OAE reapplies its patch suite, alongside APE's value anchors (ADR-0003) — but it removes any coupling to `hsie.act` continuing to exist or behave the same way. One module (`ape.act`) and one patch call site are intended to serve the entire future hack suite: new hacks add their routines inside the same `DllMain` loop rather than requiring additional `sh3.exe` patches.

## Superseded 2026-11-25: stock sh3.exe already loads every `*.act`

The premise above is wrong. Stock `sh3.exe` (OAE 1.5.2, `20360fe8…`) does scan its folder:
at `0x00401057` it `_chdir`s, then runs `_findfirst("*.act")` / `_findnext` (pattern string
at `0x0051DA8C`) and calls `LoadLibraryA` (IAT `0x0051D244`) on every match. That is how
`hsie.act`, `sh5.act` and the stock modules (`sh3sim.act`, `EnvSim.act`, …) load, which is
why none of their names appears in `sh3.exe`. The strings search missed the wildcard.

So **`ape.act` loads by being in the game folder.** No `sh3.exe` patch and no proxying is
needed, and there is no second artifact to re-verify. The patcher's deploy step (ticket 22)
is the whole install. In-play confirmation: the owner's 2026-11-25 sessions (depth-pressure
ticket 27) ran a patcher-deployed `ape.act` with no other loader, and seepage acted.
Ticket 02 is closed as not needed, and the patcher's `LoadCallSiteExists` gate is removed.
