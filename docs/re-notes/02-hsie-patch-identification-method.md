# RE method: locating h.sie/Stiebler patch sites in `.act` modules

Not a value-anchor note for one target — a reusable **technique** for finding
*where a given hsie/Stiebler patch lives* before APE can anchor on it or consider
re-patching it.

> ## ⚠ Corrected 2026-08-15 — two things below are wrong
>
> This note was written from a live-debugging account, before anyone had looked
> at the bytes. Static diffing (`03-utanker-hull-repair-signature.md`) then
> established:
>
> 1. **The patches are not in `hsie.act`.** They live in four sections h.sie
>    appended to **`sh3.exe` itself**: `.pcode` (`0x633000`, caves), `.pconst`
>    (`0x63C000`, constants/strings/**flag table**), `.pdata` (`0x63E000`,
>    writable patch variables), and a second `.text` (`0x640000`). Every mention
>    of `hsie.act` below should read `sh3.exe`.
> 2. **The debugger step is not required.** The flag table is at VA `0x63CF00`
>    (file `0x14EF00`), one byte per patch, `0xFF` = on / `0x00` = off. To
>    identify any patch's byte, build two `sh3.exe` variants with that patch on
>    vs. off and **diff them** — the single differing byte is the flag. Then
>    search the image for the flag's VA as a little-endian dword to find every
>    `cmp byte ptr [0x63CFxx], 0xff` gate. No breakpoints, no running game.
>
> The *structural* insight below — jump away to a cave, gate on a flag byte —
> is correct and is what makes the diff interpretable. Steps 1–2 are superseded
> by the diff; steps 3–4 still describe what to do once you have the gate.

## Why this is hard to just AOB-scan for

h.sie and Stiebler's patch sets are large and mature. They don't rewrite game logic
in place — the technique they use throughout is: **jump away from the original code
location, into a patch code segment (a "cave") elsewhere in the module**, run the
new/extended logic there, then jump back. So a patch's *effect* can be scattered
across a cave far from where it visibly hooks in, but its **entry point** — the
jump-away — sits right at the original call site, which is much easier to find than
the cave itself.

## The technique

1. **Find the flag table.** There is a data section containing a run of adjacent
   flag bytes (observed as a contiguous block of `FF FF FF …`) — one byte per patch,
   acting as an enable/disable checkbox the patch's jumped-away code tests before
   running its new logic. This table is the anchor to start from, not the jump-away
   site itself — it's easier to spot by eye/scan (a long contiguous run of `FF`s
   somewhere off the beaten path in a data section) than any individual patch's
   entry.
2. **Break on the right flag byte.** Set a memory-access (read) breakpoint in
   x64dbg on the flag byte you believe corresponds to the patch of interest. Toggle
   that patch's setting (whatever h.sie/Stiebler mechanism exposes it — a config
   option, a JSGME variant, etc.) between two states if you're not sure which byte
   is which, and confirm the breakpoint fires only when the toggled behavior is
   exercised.
3. **The breakpoint hit *is* the jump-away site** (or immediately adjacent to it) —
   the original code now tests the flag and conditionally jumps into the patch's
   code cave. That address is the patch's entry point: record it the way
   `01-crush-depth-damage-signature.md` §1 records code anchors (literal bytes +
   AOB with wildcards on anything that varies, uniqueness check over the module).
4. **From the jump-away, walk into the cave** to read the patch's actual new logic
   (the hull-repair amount calc, the diesel damage-chance roll, etc.) — this is
   where the value(s) APE needs to read/write/neutralize will be.

## Where this applies

- **Module scope:** h.sie's hardcode patches are largely delivered as `.act`
  modules (see `CONTEXT.md` — distinct from stock `sh3.exe`), so this hunt is
  usually against `hsie.act` (or whichever `.act` carries the relevant mod), not
  `sh3.exe` itself. Same AOB-signature discipline applies (ADR-0003): the module
  is a fixed binary per OAE build, so anchor on it, don't hardcode offsets, and
  version findings per build in `oae-builds.md`.
- **Two live candidates:** hull-repair (u-tanker mod) and diesel-damage — both
  scope tickets (`03`, `04`) have a "locating the relevant function is RE work"
  bullet; this is that work's starting method, to be spun into the first RE issue
  of each feature effort once scoped.

## A second option this hunt opens up: static re-patching instead of a runtime hook

Once a jump-away site and its cave are confirmed, there are two ways APE could act
on it, and this RE pass is a prerequisite for either:

- **Runtime, ape.act-style** (current pattern): AOB-anchor the site, read/write the
  live value from the alongside loop — no bytes on disk change. Needed when the fix
  has to react to live game state.
- **Static, patch1337-style**: if the fix is really "force this cave's fixed
  behavior/constant to X" (e.g. hull-repair amount forced to zero, a checkbox flag
  byte forced off), it may be simpler to ship a small offset-based binary patcher
  (à la [patch1337](https://github.com/OMEGARAZER/patch1337)) that edits `hsie.act`
  on disk directly — same philosophy OAE itself already uses on `sh3.exe`. That
  tool's format is a useful reference: a header line naming the target file, then
  one `OFFSET:OLDBYTE->NEWBYTE` line per edit, each verified against the expected
  old byte before writing. A dedicated design/PRD decision, not assumed here — the
  jump-away/flag-byte site this method finds is exactly the input either approach
  needs.
