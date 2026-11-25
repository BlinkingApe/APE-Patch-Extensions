# AOB pattern scanning for value anchors, not hardcoded offsets

> **Scoped by ADR-0007 (2026-08-17).** This decision governs `ape.act`'s **runtime**
> resolution of live values in a running process. It does **not** govern the GUI patcher's
> static edits to files on disk, which identify their target by whole-file SHA-256 against a
> version allowlist and then use fixed per-version offsets. See ADR-0007 for why the
> reasoning below doesn't transfer to a target that isn't running.
>
> **Status note (2026-11-25).** Not every anchor is in stock code. The clock (ticket 24)
> and the machine telegraph (ticket 31) are matched in h.sie's caves, the sections h.sie
> appends to `sh3.exe` (`docs/re-notes/09-depth-stress-runtime-anchors.md`). Those bytes
> can change with an h.sie update, where stock code cannot. The scan still fails inert on
> a miss: an unresolved clock skips every poll, and an unresolved telegraph costs only
> motor flooding damage.

APE resolves live game values (player boat, current depth, crush depth, hull HP, mission clock) at runtime by following pointer/offset chains rooted at anchor sites in the executable. The question is how to locate those anchors.

Context that shapes the decision: **stock `sh3.exe` is the fixed Ubisoft image and is never recompiled** — it loads non-ASLR at `0x00400000`, and OAE/hsie/Stiebler/NSAP are byte-level patches reapplied over it (new default values, inserted call sites), not a rebuild. So static code addresses are stable across OAE versions; what varies per launch is ASLR/heap placement of the live objects (reached through pointer chains) and, occasionally, the specific bytes at a patched site.

Decided: resolve anchors at runtime via **AOB (array-of-bytes) signature scanning** of stock sh3.exe rather than hardcoding raw addresses. Because the image is stable, the value of scanning is not "surviving a rebuild" but **self-verification**: an anchor is located by its byte signature, the static operand is extracted from the matched instruction, and the pointer chain is followed from there. If a patch update ever shifts an anchor, the scan simply misses — which, combined with APE's fail-inert rule, means the hack does nothing (and logs it) rather than silently reading a moved address and applying wrong damage. Hardcoded addresses would give no such signal.

This applies to every value APE resolves, not just this hack — it's the reason `docs/re-notes/` versions each anchor's signature and offset chain per OAE build rather than storing a single fixed address. Note the shift in framing from the original decision: AOB scanning now anchors **data reads**, because APE resolves values to read/write rather than a function to hook (ADR-0001). No `.act` module base needs resolving for this hack, since APE no longer touches the collision solver — all anchors live in stock sh3.exe.
