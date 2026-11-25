# Playable U-boat `.zon` discovery: static pass (ticket 18, depth-pressure-stress)

Target for `.scratch/depth-pressure-stress/issues/18-playable-uboat-zon-discovery.md`,
feeding ticket 16 (the crush-time patch writes `CrashSpeed = Hit_Points ÷ crush time`
into every playable U-boat class's `.zon`).

**Static only, one pass, 2026-11-25.** Read-only against two installs:

| Install | Root | `sh3.exe` md5 | `simdata.dll` md5 |
|---|---|---|---|
| OAE 1.5.2 | `C:\games\OneAlex Extended 1.5.2` | `e7e418f0797984e2bff17c6f20de9ea0` | `7e841d2a08c29333e09af43b92059c1d` |
| KC (GWX-KC 2.5) | `C:\games\GWC-KC2.5` | `0d3f1147c3cd98fe096420f316f45905` | `7e841d2a08c29333e09af43b92059c1d` (same) |

**Method:** plain reads of the unit `.cfg`s, `data/Cfg/Basic.cfg`, `data/Cfg/Flotilla.cfg`,
`data/Submarine/EnglishNames.cfg`, the JSGME `MODS/` folders, and byte-string searches
of `sh3.exe`/`simdata.dll`. The `.zon` values came from a small read-only Python scanner
(`zon_scan.py`, kept in the session scratchpad and not in the repo). It walks
`data/Submarine/*/`, parses each folder's `[Unit]` section, and reports every
occurrence of each `.zon` key with its length prefix and float value.

---

## 1. The rule

> **A class is a playable U-boat if `data/Submarine/<X>/<X>.cfg` has
> `HumanPlayable=YES` in its `[Unit]` section. Its `.zon` is
> `data/Submarine/<X>/<X>.zon`.**
> Match `<X>.cfg`/`<X>.zon` case-insensitively. Treat a missing key or `NO` as not
> playable.

Both installs yield the same 8 folders: `NSS_Uboat2A`, `NSS_Uboat2D`, `NSS_Uboat7b`,
`NSS_Uboat7c`, `NSS_Uboat9b`, `NSS_Uboat9c`, `NSS_Uboat9d2`, `NSS_Uboat21`.

### Evidence

1. **The game's own unit-cfg parser reads the key.** `simdata.dll` (byte-identical in
   both installs) holds the string `HumanPlayable` at file offset `0x1B378`, in the
   same string run as the other `[Unit]` keys it parses: `… Displacement, Mast, Width,
   Length, MaxSpeed, UnitType, Interior, HumanPlayable, AirGroup %d, AheadFlank …`.
   `sh3.exe` does not contain the string. This is an engine field, not a modder's
   comment.
2. **The unit cfgs set it.** For example, `data/Submarine/NSS_Uboat7c/NSS_Uboat7c.cfg`
   (both installs):
   ```
   [Unit]
   ClassName=SSTypeVIIC
   HumanPlayable=YES
   Interior=data/Interior/NSS_Uboat7/NSS_Uboat7
   UnitType=200
   ```
   Every `HumanPlayable=YES` class, and only those, also has an `Interior=` line and
   `[Salvo]`/`[Properties]`/`[EngineProperties]` sections. The AI classes have none of
   these.
3. **`sh3.exe` hard-codes the player class names.** It has one contiguous string table, ending
   with `SSTypeIIA` at file offset `0x12120C` in both installs: `SSTypeXXI, SSTypeIXD2, SSTypeIXC40,
   SSTypeIXC, SSTypeIXB, SSTypeVIIC42, SSTypeVIIC41, SSTypeVIIC, SSTypeVIIB, SSTypeIID,
   SSTypeIIA`. The 8 base names match the `ClassName=` values of the 8
   `HumanPlayable=YES` folders exactly. The 3 variant names (`…C40`, `…C41`, `…C42`) have
   no unit folder of their own (§3).
4. **The campaign data agrees.** In `data/Cfg/Basic.cfg`, `[SUBMARINE0..3]` lists the
   career boat types and versions: II {IIA, IID}, VII {VIIB, VIIC, VIIC/41, (KC also
   VIIC/42)}, IX {IXB, IXC, IXC/40, IXD2}, XXI {XXI}. The distinct `TypeSub`/`VersionSub`
   pairs in `data/Cfg/Flotilla.cfg` are (0,0) (0,1) (1,0) (1,1) (1,2) (2,0) (2,1) (2,3)
   in both installs. None of these is a IIB, VIIF, XIV, XXIII or IXB-resupply boat.
5. **The modders' own labels agree.** OAE's `data/Submarine/EnglishNames.cfg` groups
   the names under comments. `;Human Playable` lists exactly IIA, IID, VIIB, VIIC, IXB,
   IXC, IXD2 and XXI. `;AI Subs` lists XXIII, S, Scuka, T, 600, Gato, IIB, `VIIC/41`,
   VIIF, `Resupply Uboat` and XIV. KC's file has the same 8 names under `;Silent Hunter
   3`, with the AI U-boats under `;GWX 3.0`/`;GWX KC`.

Checking `UnitType` does not work as a rule. All submarines, Allied and German, have
`UnitType=200`. The decorations (`BARR_`, `DLF`, `HBW`) have `203`. A rule based on the
`NSS_Uboat` name prefix would also be wrong: it would wrongly include 14, 23, 2B, 7c41,
7f and 9b4.

### Non-playable U-boats (XIV etc.): leave them out

`NSS_Uboat14` (XIV "Milch Cow"), `NSS_Uboat2B`, `NSS_Uboat7c41`, `NSS_Uboat7f`,
`NSS_Uboat9b4` and `NSS_Uboat23` are AI-only in both installs on every source above. In
OAE they have `HumanPlayable=NO`. In KC most have no key at all, and 7f has `NO`. The
player can never command one, so its `CrashSpeed` only affects AI boats, which ticket 16
does not aim at. Leave them out. If they were wanted, the rule would have to switch to
the name prefix, which is exactly the assumption the ticket asked to avoid.

**Ticket 16 conflict:** its in-play criterion says "a VIIC **and a IIB**". The IIB is
not playable in either install, so that criterion should read IIA or IID.

## 2. `.zon` layout: the same in both installs

The value records sit after the **second** `ColisionableObject\0` in the file. The first
occurrence is the controller name and the second starts its data. Each record is:

```
u32 LE  record length = len(key) + 1 + 4   (= 15 for all three keys)
key     ASCII, e.g. "Hit_Points"
00      NUL
f32 LE  value
```

The records come in the order `ArmorLevel`, `Hit_Points`, `CrashDepth`, `CrashSpeed`,
`Rebound`, 0x13 bytes apart. For example, OAE `NSS_Uboat7c.zon` has
`ColisionableObject\0` at 8410 and 8465, and `0f 00 00 00 "Hit_Points" 00 00 00 fa 43`
(= 500.0) at key offset `0x213B`.

These checks held for **all 42 `.zon` files in both installs**:
- `Hit_Points\0`, `CrashDepth\0` and `CrashSpeed\0` each occur **exactly once** per file.
- The u32 before each key is always 15.
- `ArmorLevel\0` occurs many times (once per hit-sphere or box), so it is not a safe
  anchor.

The offsets differ between installs and between classes (§4), so the patcher must
search for each key and check its length prefix. It cannot use a fixed offset.

`HBW` (both installs) has no `.zon` at all. No other folder has more or fewer than one.

## 3. Variant boats share their base folder's `.zon`

`SSTypeVIIC41`/`VIIC42`/`IXC40` have no unit cfg with that `ClassName`. Two KC JSGME
mods show which files they use. `MODS/GWX - KC Player Type VIIC-41/` ships only
`data/Submarine/NSS_Uboat7c/NSS_Uboat7c.{dat,zon}`, and its readme says *"Crush depth
modified to better reflect Type VIIC/41 real life capabilities"*.
`MODS/GWX - KC Type IXC-40 with Schnelltauchback Hull/` ships
`data/Submarine/NSS_Uboat9c/NSS_Uboat9c.{dat,sim}`. So the player's VIIC/41 and VIIC/42
take their values from `NSS_Uboat7c.zon`, and the IXC/40 from `NSS_Uboat9c.zon`.
Patching the 8 base folders therefore covers all 11 player class names. The separate
`NSS_Uboat7c41` folder (`ClassName=SSTypeVIIC/41`, with a slash) is the AI flak boat.

## 4. Resulting file lists

Crush time = `Hit_Points ÷ CrashSpeed`, in seconds at full hull below crush depth (the
CONTEXT.md definition). No file is read-only. No two of the playable `.zon` files are
byte-identical, so every class has its own file.

### OAE 1.5.2 (live files, `C:\games\OneAlex Extended 1.5.2\data\Submarine\…`)

| Folder / `.zon` | ClassName | Hit_Points | CrashDepth | CrashSpeed | crush time | stock CrashDepth / CrashSpeed (JSGME backup) |
|---|---|---|---|---|---|---|
| NSS_Uboat2A | SSTypeIIA | 400 | 251 | 0.8 | 500 s | 230 / 0.08 |
| NSS_Uboat2D | SSTypeIID | 400 | 251 | 0.8 | 500 s | 230 / 0.08 |
| NSS_Uboat7b | SSTypeVIIB | 500 | 311 | 0.8 | 625 s | 333.2 / 0.03 |
| NSS_Uboat7c | SSTypeVIIC | 500 | 331 | 0.8 | 625 s | 350 / 0.08 |
| NSS_Uboat9b | SSTypeIXB | 550 | 311 | 0.8 | 687.5 s | 350 / 0.08 |
| NSS_Uboat9c | SSTypeIXC | 550 | 331 | 0.8 | 687.5 s | 375 / 0.1 |
| NSS_Uboat9d2 | SSTypeIXD2 | 550 | 331 | 0.8 | 687.5 s | 375 / 0.1 |
| NSS_Uboat21 | SSTypeXXI | 600 | 410 | 0.8 | 750 s | 410 / 0.03 |

The live OAE values come from **the user's own JSGME mod `[TEST] - Sub Crush Speed
Modded`**. It is enabled (`MODS/JSGME.ini`: `<TEST] - Sub Crush Speed Modded=6`), and
its install log lists all 8 `.zon` files above **plus `NSS_Uboat7c41.zon`** (AI). The
pre-mod originals are in `MODS/!BACKUP/data/Submarine/<X>/<X>.zon.[TEST] - Sub Crush
Speed Modded`, which is the last column. Hit_Points is unchanged by the mod. In the
backup, AI `NSS_Uboat7c41` is 500 / 240 / 2.0. No other enabled OAE mod touches a
`data/Submarine` `.zon`.

Excluded in OAE: 600_Class (HP 220 / CD 100 / CS 2), BARR_ (400/300/0), DLF (20/300/0.01),
HBW (no `.zon`), NSS_Gato (220/330/2), NSS_Uboat14 (300/303/2), NSS_Uboat23 (150/214/0.1),
NSS_Uboat2B (150/214/2), NSS_Uboat7c41 (500/371/0.8, test mod), NSS_Uboat7f (169/315/2),
NSS_Uboat9b4 (300/303/2), SClass (200/300/1), Scuka (120/60/2), TClass (180/100/2).

### KC (`C:\games\GWC-KC2.5\data\Submarine\…`)

| Folder / `.zon` | ClassName | Hit_Points | CrashDepth | CrashSpeed | crush time |
|---|---|---|---|---|---|
| NSS_Uboat2A | SSTypeIIA | 400 | 228.2 | 2 | 200 s |
| NSS_Uboat2D | SSTypeIID | 400 | 263.2 | 2 | 200 s |
| NSS_Uboat7b | SSTypeVIIB | 500 | 333.2 | 2 | 250 s |
| NSS_Uboat7c | SSTypeVIIC | 500 | 371 | 2 | 250 s |
| NSS_Uboat9b | SSTypeIXB | 550 | 375.2 | 2 | 275 s |
| NSS_Uboat9c | SSTypeIXC | 600 | 375.2 | 2 | 300 s |
| NSS_Uboat9d2 | SSTypeIXD2 | 550 | 375.2 | 2 | 275 s |
| NSS_Uboat21 (cfg is `NSS_Uboat21.CFG`) | SSTypeXXI | 600 | 410.2 | 2 | 300 s |

No enabled KC mod touches a `.zon`, so these are the values as KC is installed.

Excluded in KC: 600_Class (240/100/2), BARR_ (10000/300/0), DLF (20/300/0.01), HBW (no
`.zon`), NSS_Gato (280/100/2), NSS_Uboat14 (300/303/2), NSS_Uboat23 (300/214/0.1),
NSS_Uboat2B (150/214/2), NSS_Uboat7c41 (350/346/2), NSS_Uboat7f (169/315/2),
NSS_Uboat9b4 (300/303/2), NSS_Undine (600/168/2), SClass (350/300/1), Scuka (200/60/2),
TClass (280/100/2).

### HBW and NSS_Undine

- **HBW** is in **both** installs, not only KC. Its cfg is `ClassName=HBW`,
  `UnitType=203`, and `EnglishNames.cfg` names it `HBW=Whale` (OAE) or `HBW=Humpback
  Whale` (KC). It is a decoration with no `.zon`, so it is never a target.
- **NSS_Undine** (KC only) is `ClassName=SSUndine`, `UnitType=200`, with no
  `HumanPlayable` key. `EnglishNames.cfg` names it `SSUndine=U Class Submarine`, and its
  roster file is `data/Roster/British/Submarine/Uclass.cfg` (`DisplayName=U Class`,
  boats "P32", "HMS Umpire", "HMS Unbeaten"). It is the **British U-class**, an Allied
  AI submarine and not a German U-boat. It is excluded.

## 5. Things the patcher should know

1. **KC breaks the default of 625.** Ticket 16 sets the default to 625 s so that "a first
   run leaves a VIIC unchanged". That only holds in OAE with the user's test mod
   enabled. In stock OAE (backup) the VIIC is 500 ÷ 0.08 = **6250 s**. In KC it is
   500 ÷ 2 = **250 s**. A first run at 625 s would make KC boats survive 2.5× longer and
   stock-OAE boats 10× shorter.
2. **The OAE target files are already owned by a JSGME mod.** If the user later disables
   `[TEST] - Sub Crush Speed Modded`, JSGME restores its own backup over APE's write.
   APE's drift check would then show "changed by another mod", which is correct, but
   APE's `.ape-orig` would hold the *test-mod* bytes, not OAE stock. Enabling or
   disabling JSGME mods while APE's patch is applied makes "restore" mean different
   things.
3. **The unit `.cfg` has its own `CrashDepth`.** `[Properties]` has `CrashDepth=70;meters`
   / `80;meters`. That is a different, much shallower value from the `.zon`
   `CrashDepth` float (crush depth). The patcher must not read the cfg's value as crush
   depth.
4. **Case differs between files.** KC's XXI cfg is `NSS_Uboat21.CFG`, and OAE's XXI model
   is `NSS_Uboat21.DAT`. In every folder the stem matches the folder name. Match names
   case-insensitively.
5. Live OAE `.cfg` files also come from mods (`[GAMEPLAY] - Historical Engine Presets`,
   `[BASE] - Devbuild`, `New VIIB Final`). Every backed-up original still says
   `HumanPlayable=YES`, so the rule does not change with those mods on or off.
6. No `.zon` is read-only, and none is shared between folders. No playable `.zon` lives
   outside `data/Submarine/<X>/`. The `data/Library/*GUNS_SUB*.zon` and
   `AI_NSS_Uboat_SNK_eqp.zon` files are equipment files, not hulls.

## 6. Open caveats

- `HumanPlayable`'s default when the key is missing was not checked in `simdata.dll`'s
  code. Every class without the key is AI by all the other evidence in §1, so "missing =
  not playable" is safe for these two installs. It is an inference, not a disassembly.
- The link from `SSTypeVIIC41`/`VIIC42`/`IXC40` to the base folder's `.zon` (§3) comes
  from what the KC mods replace, not from tracing the loader. It has not been confirmed
  in game.
- "Stock OAE" values are JSGME's backup taken when the test mod was enabled. That is
  OAE as installed at that moment, not a verified copy of the OAE 1.5.2 release.
