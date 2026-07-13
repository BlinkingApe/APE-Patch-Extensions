# ape.act is loaded via a dedicated sh3.exe patch, not by proxying hsie.act

OAE sh3.exe already loads two custom `.act` modules, `hsie.act` and `sh5.act` (NSAP). Inspection of the actual install (`file`, `objdump`, and `strings` in both ASCII and UTF-16LE) showed both are ordinary PE32 DLLs with empty export tables that do all their work in `DllMain`, and that neither filename appears as a literal string anywhere in `sh3.exe` or `sh3.dll`. This means their loading isn't a generic "scan the folder for `*.act`" mechanism — each is a hand-patched `LoadLibrary` call site added directly to `sh3.exe`'s code.

Two ways to get `ape.act` loaded followed from this: proxy `hsie.act` (rename the original, forward to it via `DllMain` — since it has no exports, no export-forwarding is needed, and this requires zero further changes to `sh3.exe`), or patch `sh3.exe` with a new, dedicated `LoadLibrary` call site for `ape.act`.

Chose the dedicated patch. This means a second artifact (the new call site) needs re-verifying on every OAE rebuild, alongside the target function's AOB signature — but it removes any coupling to `hsie.act` continuing to exist or behave the same way. One module (`ape.act`) and one patch call site are intended to serve the entire future hack suite: new hacks add hook-install calls inside the same `DllMain` rather than requiring additional `sh3.exe` patches.
