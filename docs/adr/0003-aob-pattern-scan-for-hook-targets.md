# AOB pattern scanning for hook targets, not hardcoded offsets

OAE sh3.exe is rebuilt occasionally as its upstream mods (h.sie, Stiebler, NSAP) update, which can shift code addresses between rebuilds. Prior personal workflow relied on hardcoded offsets found via Cheat Engine/x32dbg, which then had to be manually re-found after every rebuild.

Decided: resolve hook target addresses at runtime via AOB (array-of-bytes) signature scanning instead of hardcoding an offset. A rebuild then only requires re-verifying that the signature still matches (and updating it if not), rather than re-discovering the function from scratch. This applies to every hook in the suite, not just the crush-depth damage function — it's the reason `docs/re-notes/` versions signatures per OAE build rather than storing a single fixed address.
