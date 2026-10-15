# Diesel damage: resolve `pow` by name so the game no longer needs Microsoft's own msvcr71 under Wine

Type: task
Status: ready-for-human

## Blocked by

None.

## Problem

Linux (Wine/Proton) players report in-game crashes unless `msvcr71.dll` is
switched to the native (game-folder) copy, e.g. `WINEDLLOVERRIDES="msvcr71=n,b"`.

**Root cause, confirmed statically 2026-10-15.** h.sie's `pow` thunk at
`0x6371A8` does not call `pow` through an import. It computes the address:

```
006371BB  a1 78d65100     mov  eax, [0x51D678]   ; IAT slot: MSVCR71.dll!atol
006371C0  2d 50dd0000     sub  eax, 0xDD50
006371C5  ff d0           call eax               ; "pow"
```

In Microsoft's `msvcr71.dll` 7.10.3052.4 (the copy every surveyed install ships,
sha256 `8094af5e…`), `atol` is at RVA `0x11D80` and `0x11D80 − 0xDD50 = 0x4030`,
which is **exactly the exported `pow`**. The address arithmetic only works
against that one binary layout. Wine's builtin `msvcr71` is a different binary,
so `atol − 0xDD50` lands on arbitrary code and the game crashes. (Correction
to `docs/re-notes/04-diesel-damage-signature.md` §3: `pow` *is* exported by
msvcr71. sh3.exe just doesn't import it, which is why h.sie used the offset.)

**Why it shows up as an APE problem.** The thunk's only callers are in the
diesel-damage cave: `0x635117`, plus `0x63582C` in h.sie's original TC helper,
which APE's `tc-helper-rewrite` removes. h.sie's Diesel Damage flag (index 20)
is off by default in OAE 1.5.2 and GWX 3.0. APE's diesel-damage patch turns it
on, and with APE's trampoline every non-astern tick reaches
`0x635117 → 0x6371A8`. So **enabling APE's diesel damage is what makes the game
run this code at all**. Hull repair does not use it.

**Nothing else in h.sie's code relies on a DLL layout.** A scan of every
import-slot reference in `.pcode` and the second `.text` (`0x640000`) found
only normal `call [slot]` uses, apart from this one site. The thunk bytes,
callers and import-slot addresses are identical in KC 2.5, OAE 1.5.2,
OAE 2.0 TEST_1 and GWX 3.0 Gold (all h.sie `v 1.6E1`).

## Fix: call `GetProcAddress(GetModuleHandleA("MSVCR71.dll"), "pow")`

sh3.exe already imports both from KERNEL32 (not the game's own `Kernel.dll`):
`GetModuleHandleA` at `0x51D248` and `GetProcAddress` at `0x51D1DC`. `MSVCR71.dll`
is a static import of sh3.exe, so it is always loaded. Both Microsoft's and
Wine's msvcr71 export `pow` by name.

- **Windows: behaviour is identical.** `GetProcAddress` returns the same address
  the offset produced (RVA `0x4030`), so the same function gives bit-identical results.
- **Wine: Wine's own `pow` is called.** No DLL override is needed.
- **Side benefit:** a Windows install with a different msvcr71 build (for example the
  7.10.6030 SP1 runtime) would break the offset trick too. Looking `pow` up by name removes
  that weakness.

Two new `fixed-rewrite` sites under the `diesel-damage` patch. Both were
hand-assembled and checked by disassembly (capstone):

**Site A: the thunk's address computation, at VA `0x6371BB` / file `0x1491BB`**

```
original     a178d651002d50dd0000      mov eax,[atol]; sub eax,0xDD50
replacement  e8403e00009090909090      call 0x63B000; nop x5
```

The original and replacement share no bytes, so the run has no invariant context. Widen the
site to start at `0x6371A8` (the whole thunk). That makes the unchanged
prologue/marshalling bytes `558bec83ec10d9450cdd5c2408d94508dd1c24` (offset 0)
and the tail `ffd083c410c9c20800` (offset 29) the context, matching how
`tc-helper-rewrite` anchors on `558bec`.

**Site B: the resolver, in the `.pcode` tail cave at VA `0x63B000` / file `0x14D000`**

```
0063B000  68 2cb06300     push 0x63B02C          ; "pow"
0063B005  68 20b06300     push 0x63B020          ; "MSVCR71.dll"
0063B00A  ff15 48d25100   call [GetModuleHandleA] ; consumes "MSVCR71.dll"
0063B010  50              push eax                ; hModule
0063B011  ff15 dcd15100   call [GetProcAddress]   ; (hModule, "pow") -> eax
0063B017  c3              ret
0063B018  cc x8
0063B020  "MSVCR71.dll\0"
0063B02C  "pow\0"
```

Replacement (48 bytes):
`682cb063006820b06300ff1548d2510050ff15dcd15100c3cccccccccccccccc4d5356435237312e646c6c00706f7700`
Original: 48 × `00`.

- The `.pcode` tail from `0x63948C` to `0x63C000` is all `00` in all four surveyed
  installs. No absolute or rel32 reference anywhere in the image points into it.
- The resolver is stateless: it needs no writable cache, and it only clobbers
  `eax/ecx/edx`, which the original `call eax` into the CRT clobbers anyway. The
  FPU stack is empty at that point because the arguments are already stored to
  memory.
- Cost: two KERNEL32 calls per diesel tick (about one per real second). This is
  negligible, so don't add a cache.
- Context for the cave: there are no invariant bytes. Either leave `context: []`
  as `reverse-telegraph-skip-jmp` does, or widen the run a few bytes past `0x63B030`
  and anchor on trailing `00`s that stay zero. Implementer's call. Note that
  `Verify` only checks context bytes.

## Implementation

1. `tools/re-static/gen_manifest.py`: emit the two sites above under
   `diesel-damage`, then regenerate `data/manifest/ape-manifest.json`.
2. `tests/patcher_gui/PatchSessionTests.cs`: add round-trip coverage for both sites.
   Apply, then uncheck to restore the originals byte-exactly. Mirror the existing diesel
   `.pcode` site tests.
3. Upgrade path: a v1.13-patched install reads as applied, because `IsApplied` is true
   when *any* site holds an APE value. Re-applying writes the new sites. Release notes
   should tell Linux players to press Apply once after updating.
4. Update `docs/re-notes/04-diesel-damage-signature.md` §3 (the thunk, plus the
   "unexported" correction) and add a §-entry for the resolver.
5. README: add a short Linux/Wine note. Until this ships, the workaround is
   `msvcr71=n,b`. After it ships, no override is needed.

## Verification

- **Windows (dev machine):** apply to KC 2.5 and confirm diesel wear still fires at the
  forced-rate rig from umbrella ticket 12. A CE breakpoint at `0x6371C5` should
  show `eax == msvcr71!pow`.
- **Wine (needs a Linux player, ready-for-human):** with the msvcr71 override
  *removed*, run with APE diesel damage on, ahead and under time compression,
  long enough for several diesel ticks. Expect no crash.

## Out of scope

- **Rewriting `pow` as inline x87 (`fyl2x`/`f2xm1`/`fscale`).** It would remove the
  CRT entirely, but `pow(0, y)` and the negative-base NaN cases would have to be special-cased
  by hand. Results would also stop being bit-identical to the CRT on Windows.
- **Adding `pow` to sh3.exe's import table.** This needs PE restructuring that the
  site-based patcher has no model for.
- **`ape.act`'s CRT linkage.** It is not shipped yet. When it is, link with `/MT` so it
  carries no runtime DLL dependency of its own.

## Comments

**2026-10-15 — implemented; in-game verification left for humans.**

- Two `fixed-rewrite` sites under `diesel-damage` in `gen_manifest.py`:
  `pow-thunk-call-resolver` (0x6371A8, whole 38-byte thunk; context = the
  19-byte head at 0 and the 9-byte tail at 29) and `pow-resolver-cave`
  (0x63B000, widened to 64 bytes; the 16 trailing zeros at offset 48 are its
  context). Originals re-read from all three reference exes; capstone confirms
  the call lands on 0x63B000 and the resolver matches the listing above.
- Tests: round trip for both sites (plus a check that the thunk's rel32 hits
  the resolver site's VA), and the upgrade path: a v1.13-shaped install reads
  as applied and an unchanged Apply writes exactly the two pow sites.
- Docs: re-notes 04 §3 correction + new §3c; README Linux/Wine note.
- Still open (why this is `ready-for-human`): the Windows CE breakpoint at
  0x6371C5 / forced-rate rig check, and the Wine run with the msvcr71 override
  removed. The release notes for the next version must tell Linux players to
  press Apply once after updating — there is no in-repo changelog to carry it.
