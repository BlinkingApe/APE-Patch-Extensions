# `hsie.ini` clobbers `.pconst` at load — what does the patcher do about it?

Type: grilling
Status: open

## Blocked by

None. (Independent of 07/08, but its answer constrains both — sequence it first
if you can.)

## Question

Found while resolving `04-diesel-damage-patch-scope.md`. `hsie.act` reads
`./hsie.ini` at load, `VirtualProtect`s h.sie's `.pconst` section, and writes 24
floats into it from the ini's `ValN`/`AdrN` table. Proven, not inferred: the
on-disk exe holds 0.15/0.10/0.03 where `hsie.ini` holds 0.35/0.20/0.05, and the
ini is what applies in play. See `docs/re-notes/04-diesel-damage-signature.md` §0.

**Consequence: any static byte APE's GUI patcher writes to one of these 24
addresses is silently overwritten at runtime.**

```
0x63C000, 0x63C084, 0x63C088, 0x63C08C, 0x63C090, 0x63C094, 0x63C098,
0x63C0A4, 0x63C0A8, 0x63C0AC, 0x63C0B0, 0x63C0B4, 0x63C0B8, 0x63C0BC,
0x63C0C0, 0x63C0C4, 0x63C0C8, 0x63C0CC, 0x63C0D0, 0x63C0D4, 0x63C0D8,
0x63C0DC, 0x63C0E4, 0x63C0E8
```

This is nastier than an ordinary version mismatch: **verify-before-write cannot
detect it.** The on-disk bytes are exactly what the patcher expects right up
until `hsie.act` loads, so the write succeeds, the verification passes, the GUI
reads back the value it just wrote — and the setting still does nothing in game.
Ticket 02 settled that the binary is the only store; for these addresses the
binary is a *lying* store.

Decide:

- **Does the manifest carry a forbidden-write region?** Ticket 08 defines the
  manifest format; this looks like a required field (`clobbered-by: hsie.ini`)
  rather than an optional one. If so, is the list hardcoded, or derived by
  parsing `hsie.ini`'s `AdrN` table at patch time — noting the user can edit that
  file, so the list is not actually static?
- **What does the patcher do when a feature needs one of these addresses?**
  Refuse it, route it to a runtime write via `ape.act` instead, or write
  `hsie.ini` itself (which crosses the same "APE doesn't own h.sie's settings"
  line that ticket 10 is about)?
- **Does this generalise?** `hsie.act` is one known runtime writer. Is there a
  cheap way to establish that nothing *else* rewrites APE's write targets at
  load — or does every manifest site now need a "proven stable at runtime"
  claim rather than just an on-disk verification?
- **Does it change ticket 02's settings model?** "The binary is the only store"
  survives intact for addresses nobody else touches. For these it does not.
  Is the answer a carve-out, or does the model need restating?

Known not to be affected: the hull-repair site (`0x63C404`, and the `push 0x10` /
`add eax, 5` operands at `0x633650`/`0x633655`) is outside the list, so ticket 03
stands unchanged.

## Update 2026-08-15 — ticket 04 supplies a worked precedent for bullet 2

The reopened `04-diesel-damage-patch-scope.md` resolved onto the **third** option
in the "what does the patcher do" bullet above: **write `hsie.ini` itself.**

The diesel feature's entire payload is eight `ValN` keys, all inside the clobbered
region, and h.sie built that file precisely as the tuning surface for them. So for
that feature the clobber is not an obstacle at all — it is the delivery mechanism,
and the ini is a *better* store than the binary (human-readable, hand-revertible,
and it is what actually wins at runtime).

That does not resolve this ticket; it constrains it. Three of the four bullets are
now sharper rather than answered:

- **The forbidden-write region is real but one-sided.** Static writes to those 24
  addresses are futile; writes to the *ini keys that feed them* are canonical. The
  manifest arguably needs both a `clobbered-by: hsie.ini` marker **and** a
  redirect — "this site is authored via `hsie.ini` key `ValN`" — rather than a
  plain refusal.
- **Ticket 02's model does need restating, not carving out.** "The binary is the
  only store" is false as soon as one feature's store is a text file. Diesel makes
  `hsie.ini` a third write target alongside `sh3.exe` and `ape.act`.
- **A surgical INI writer is now a required patcher component.** OAE ships heavily
  customised values for the *other* patches (torpedo failure, weather, snorkelling,
  collision); the writer must edit named keys in place and preserve comments,
  ordering, and everything it does not own. Note also that the `AdrN` table is
  itself user-editable, which is the "the list is not actually static" worry above
  made concrete.

Still open here, and untouched by ticket 04: whether the address list is hardcoded
or parsed at patch time, and whether *other* runtime writers besides `hsie.act`
need ruling out.
