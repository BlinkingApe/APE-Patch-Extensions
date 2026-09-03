# Settle attribution stance for reverse-engineered patch data

Type: grilling
Status: resolved

## Question

APE's manifest embeds byte-level offsets and replacement bytes reverse-engineered from h.sie's/OAE's hardcode patches (`tools/re-static/gen_manifest.py`, `data/manifest/ape-manifest.json`) — its core content, not incidental clutter. Settle APE's attribution/licensing stance toward h.sie, Stiebler, NSAP, and OAE: what credit or disclaimer is owed, and whether redistributing this derived data is a risk that should be explicitly flagged in the public docs rather than silently accepted.

## Answer

**Risk stance: low, treat as functional data, no hedging in the docs.** The manifest stores per-patch a handful of original bytes (h.sie/OAE's compiled machine code, just enough to verify-before-write) and APE's own replacement bytes/offsets — the same shape as a binary-diff/patch format (e.g. patch1337), not a copy of the executable or any creative expression. h.sie/Stiebler/NSAP have no formal license terms (typical for this modding scene — no LICENSE file, just forum posts), so there's no known explicit term this violates. No legal-review disclaimer is added; the README carries a plain credit line instead.

**Credit scope: h.sie and Stiebler only, plus `sh3.exe` as the target binary — not OAE, not GWX/KC, not NSAP.** APE's manifest derives data specifically from h.sie's hardcode patches (diesel damage, the reverse-telegraph/tc-helper rewrites, the diesel message gate) and Stiebler's u-tanker hull-repair mod — those are the two mod authors whose patch logic APE actually reads/rewrites. OAE is just the specific bundle/build APE happens to target (already documented in CONTEXT.md's glossary as "OAE sh3.exe"); GWX/KC and NSAP aren't sources APE derives manifest data from, so they don't belong in the credit line.

**Docs impact:** README's attribution section credits h.sie and Stiebler (as the patch authors) and names `sh3.exe` as the target binary, with no disclaimer/hedge language — unblocks [Draft public-facing docs](04-public-docs.md).
