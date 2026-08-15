# RE notes

Reverse-engineering findings for the value anchors and patch sites APE uses in OAE
sh3.exe, **versioned per OAE build** (ADR-0003). Stock sh3.exe is never recompiled,
but OAE reapplies its upstream byte-patch suite (h.sie/Stiebler/NSAP) as those mods
update, which can shift bytes at a patched site — so every artifact here records the
exact build it was verified against, and a patch-suite reapplication means
re-verifying (and, if needed, updating) the signature rather than re-discovering it
from scratch.

## What lives here

- `oae-builds.md` — the registry of OAE sh3.exe builds we've fingerprinted
  (SHA-256, size, human label/date). Every note below references a build by its
  short fingerprint so "which binary was this true for?" is never ambiguous.
- One file per value-anchor set / patch site, e.g.
  `01-crush-depth-damage-signature.md`, `02-ape-act-load-call-site.md`.
- `02-hsie-patch-identification-method.md` — reusable technique (not a per-target
  note) for locating h.sie/Stiebler patch entry points, ahead of anchoring a
  specific target. ⚠ Carries a correction banner: its original `hsie.act` premise
  and its debugger-based steps are both superseded by static diffing.
- `03-utanker-hull-repair-signature.md` — the u-tanker mod fully mapped (h.sie's
  appended `sh3.exe` sections, the patch flag table at `0x63CF00`, and the
  hull-repair site). Also the worked example of the static-diff method.

## Conventions

- **Fingerprint, don't trust version strings.** OAE version labels are informal;
  the load-bearing identifier is the SHA-256 of the exact `sh3.exe` bytes.
- **AOB signatures use `??` for wildcard bytes** (relative call/jump operands,
  absolute addresses, and anything observed to vary across builds). Record *why*
  each wildcard is a wildcard.
- **Every signature must be proven unique** in the target module at scan time
  (exactly one match), and that check recorded.
