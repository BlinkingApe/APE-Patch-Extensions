# 36: Depth stress does nothing on GWX-KC: no clock-mirror cave in its `sh3.exe`

**Type:** task

**Blocked by:** none

**Status:** resolved (code and static check; in-play check on KC is the owner's)

## What was reported (owner, 2026-11-25)

No leakage below 100 m on GWX-KC 2.5 (`C:\games\GWX_KC_2.5`). The same build works on OAE.

## Diagnosis (static, 2026-11-25)

`ape.act` loads on KC, but `ape_act.log` shows the same thing in every session:

```
[depth-stress] anchor 'clock' unresolved (no match): every poll will skip
[depth-stress] skip: an anchor did not resolve (x299 since last logged)
```

So none of depth stress runs on KC: no seepage, no hull multiplier, no motor flooding damage,
at any depth. This is the "fail inert" path of note 09, working as designed.

All ten `ape.act` signatures were scanned over both exes (executable sections, as `aob.hpp` does).
Nine resolve once, to the same operand, on both: boat root, depth root, engine root, engine
block offset, zone list head, zone compartment table, telegraph (`0x635065` -> `0x63E1D4`),
report root and string table. **Only `clock` fails on KC.**

The clock anchor (`A3 ?? ?? ?? ?? 89 86 50 01 00 00 E9`) is h.sie's clock-mirror cave, not
stock code. KC's `sh3.exe` doesn't have it:

| | OAE 1.5.2 (`20360fe8`) | GWX-KC 2.5 (`e2b190e1`, mtime 2026-11-25 19:13) |
|---|---|---|
| `0x004C2115` (file `0xC2115`) | `E9 AD AE 05 00 90`: `jmp 0x51CFC7; nop` | `89 86 50 01 00 00`: `mov [esi+0x150],eax` (stock) |
| `0x0051CFC7` (file `0x11CFC7`) | `A3 80 FF 63 00 89 86 50 01 00 00 E9 44 51 FA FF` | zero-filled slack (0x39 bytes checked) |
| `0x0063FF80` mirror (`.pdata`, file `0x151F80`) | written by the cave | zero, and named by no code |
| Second `.text` at `0x640000` | present (`0xE0000020`, 0x8000) | absent |

`.pdata` is `0xC0000040` (read/write) in both, so the mirror is writable at runtime on KC too.
KC's `hsie.ini` says h.sie V16B1, the same as OAE's, but its exe has fewer of h.sie's caves.

**Note 09 is wrong for this install.** Its uniqueness table says "GWC-KC 2.5, `dc8bd5dc`"
matched the clock once. The exe the owner plays (`e2b190e1`) does not match it. Either that row
came from a different KC exe, or it was wrong.

## Fix (owner chose, 2026-11-25): the patcher adds the cave when it's missing

The patcher writes OAE's exact bytes into KC's exe. `ape.act` doesn't change: the existing
clock signature then matches, and `0x63FF80` carries the stock clock as it does on OAE.

| Where (VA / file offset) | Expect before (KC) | Write |
|---|---|---|
| `0x004C2115` / `0xC2115` | `89 86 50 01 00 00` | `E9 AD AE 05 00 90` |
| `0x0051CFC7` / `0x11CFC7` | 16 bytes of `00` | `A3 80 FF 63 00 89 86 50 01 00 00 E9 44 51 FA FF` |

The cave stores the clock to the mirror, does the stock store, and jumps back to `0x4C211B`.
These are the bytes OAE 1.5.2 and `data/exe_with_hsie_patches/sh3_vanilla.exe` already carry.

## Acceptance

- [ ] Offered only when the clock anchor doesn't resolve and **both** sites hold exactly the
      "expect before" bytes. Otherwise (the cave already there, or anything else at either site)
      it isn't offered and nothing is written. OAE never sees it.
- [ ] `sh3.exe` is backed up first and reverts byte-exact. Follow the `.ape-orig` pattern of
      `MotorZoneFix.cs`, or fold it into the existing `sh3.exe` patch/revert path in
      `PatchSession.cs` if that fits better. Pick one and say which.
- [ ] The patcher's own read-back treats the cave as applied (the clock signature resolves to
      `0x63FF80`), and Review shows it.
- [ ] Tests: a KC-shaped exe gets the cave, an OAE-shaped exe is left alone, an exe with
      unexpected bytes at either site is refused, and revert restores the original bytes.
- [ ] Statically: after patching a copy of KC's exe, all ten `ape.act` signatures resolve once.
      Don't patch the owner's install to check this.
- [ ] In play (owner, KC): `ape_act.log` no longer says `anchor 'clock' unresolved`, and
      seepage starts below 100 m.
- [ ] Note 09's uniqueness table gets a corrected KC row (`e2b190e1`: clock 0 before the
      cave, 1 after), and the README's GWX-KC notes mention the cave.

## Open points for the implementer

- Is anything else in KC already using the `0x51CFC7` slack or `0x63FF80`? Static check:
  both are zero, and no code names `0x63FF80`. h.sie's `sh5.act` (moon mod) has a table of
  `0x63FFxx` addresses, per note 08 §3. KC's install has no `sh5.act`, so that clash doesn't
  arise there.
- When h.sie's options tool (`HsieOptionsSelector.exe`) rewrites `sh3.exe`, does it put back
  `0x4C2115` and remove the cave? If so, the patcher's read-back should show the cave as
  missing again, not crash or show it as applied.

## Comments

### 2026-11-25: built

- **Picked: the `.ape-orig` pattern**, as a new opt-in **Depth stress clock fix** checkbox under
  Depth stress (`src/patcher_gui/ClockCave.cs`). It is not folded into the manifest's sh3.exe
  sites, because OAE's own cave is byte-identical to APE's: only the copy can tell them apart.
  On the cave's first write, APE writes `sh3.exe.ape-orig` (the whole exe as it was), then the
  two sites. That copy is both the backup and the marker that the cave is APE's. Revert writes
  back stock's store and 16 zero bytes, then deletes the copy, so it leaves alone any other
  feature written into sh3.exe since.
- **States.** `ClockCave.Classify` sorts every exe into one of these:
  - `NotNeeded` (the clock signature already matches: OAE, so no checkbox).
  - `Missing`: no match, `89 86 50 01 00 00` at `0xC2115`, 16 zero bytes at `0x11CFC7`. Offered.
  - `Applied`: copy present, both sites hold the cave, and the signature matches once, to
    `0x63FF80`.
  - `Unrecognised`: no match and anything else at either site. Not offered, and the Depth
    stress note says depth stress will do nothing here.
  - `ChangedByOtherMod`: copy present, sites in neither state. Not offered, and unchecking
    writes nothing.
  - `Unreadable`: the copy isn't a pre-cave exe.
  
  The signature is counted over the whole file, a superset of ape.act's executable-section
  scan. On the four real exes the counts agree.
- **Review** shows the fix in game terms. It names no file and no address.
- **If the Depth stress clock fix is unchecked**, the Depth stress note warns that depth
  stress will do nothing on this install.
- **Rollback:** if sh3.exe can't be put back after a failed apply, its copy stays, so the
  cave still reads as APE's and can be taken out.
- **Tests:** `tests/patcher_gui/ClockCaveTests.cs`, 14 cases. Covered:
  - A KC-shaped exe is offered the cave, and applying it writes the cave and keeps a copy.
  - Read-back shows the cave applied, resolving to `0x63FF80`.
  - Revert is byte-exact, and keeps a hull patch written after the cave.
  - An OAE-shaped exe is never offered the cave or written.
  - Unexpected bytes at either site are refused.
  - The other cases: a tool putting stock code back, an exe changed after the cave, write
    failures, a copy appearing after opening, and Review.
- **Static acceptance, done on a scratch copy of the owner's KC files.** The owner's install
  was only read. The real `PatchSession` ran on a copy of `e2b190e1`:
  - Before: nine of the ten `ape.act` signatures resolve once, and clock has 0 matches.
  - After: all ten resolve once, and clock resolves to `0x63FF80`.
  - Unchecking: sh3.exe is byte-identical to the original and the copy is gone.
- **Docs:** note 09's table has a corrected KC row (`e2b190e1`: clock 0 before the cave, 1
  after). README.md and readme.html mention the fix for GWX-KC. The v1.14 PDF is rebuilt.

**Open points:**
- *Does anything else use the `0x51CFC7` slack or `0x63FF80`?* Checked statically only: both
  are zero in KC's exe and nothing names `0x63FF80`. `0x51CFC7` is inside `.text` (which ends
  at `0x51D000`), so the cave is executable.
- *HsieOptionsSelector rewriting sh3.exe:* not run. If it puts the stock store back, the
  cave reads as `Missing` and can be offered again, with the first copy reused (tested). If
  it writes anything else there, the cave reads as `ChangedByOtherMod` and is left alone.
  Nothing crashes, and nothing shows as applied when it isn't.

**Known limits:**
- The copy is checked only at the two sites. A KC copy left beside a swapped-in OAE exe
  would read as `Applied`, and unchecking would remove OAE's own cave.
- A copy kept from an earlier apply is not refreshed, so it can be older than the current
  exe. Revert never reads it, so this changes nothing.

**Still open (owner, in play on KC):**
- Check the Depth stress clock fix as well as Depth stress, and apply.
- `ape_act.log` should no longer say `anchor 'clock' unresolved`.
- Seepage should start below 100 m.

### 2026-11-25: the clock code now goes in with depth stress (owner's request)

- There is no separate checkbox any more. Checking **Depth stress** on an exe that lacks h.sie's
  cave (state `Missing`) also writes the cave and keeps `sh3.exe.ape-orig`. Unchecking depth
  stress takes out APE's cave and deletes the copy.
- If `ape.act` is already deployed but the cave is missing (a KC install patched with v1.14
  before this fix), opening APE and pressing Apply adds the cave.
- Review shows it inside the Depth stress group, with no extra line. The apply summary says
  "sh3.exe clock code added/removed".
- The Depth stress note warns only when APE can't add the cave (`Unrecognised`,
  `ChangedByOtherMod`, `Unreadable`).
- The player-facing text no longer mentions OAE: it calls this h.sie's clock code.
- In the same commit, the leakage defaults became **0.35 / 0.6 %/min** (tier 1 / tier 2), set
  by the owner. Changed: `kDefaultConstants`, `PatchState`, the leakage fields (2 decimals,
  0.05 steps), ADR-0012 (as a note) and readme.html.
