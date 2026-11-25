# 33: Starboard motor damage shows in the bow quarters (OAE `Zones.cfg`)

**What was reported (owner, 2026-11-25):** when motor flooding damage (ticket 31) damages
the electric motors, the battery compartment on F7 also turns red. Nothing there is
damaged, so the red seems to stay. With the damage-control team set to the bow quarters,
the game later reports "Stb engine repaired". Seen on a Type XXI and on the lab's IIA.

**Blocked by:** none

**Status:** resolved (owner chose both: patcher fix and README note, 2026-11-25)

## Answer: OAE's damage model parents the starboard motor to the bow quarters

OAE's `data/Zones.cfg` (GWX 1.9 damage model) has this:

| Zone | Vanilla SH3 | OAE 1.5.2 |
|---|---|---|
| `[ElectricEngine1]`, zone 22, the starboard motor | `Father=30` (ElectricEngineRoom) | **`Father=24`** (Quarters1, the bow quarters) |
| `[ElectricEngine2]`, zone 56, the port motor | `Father=30` | `Father=30` |

So the game treats the starboard motor as part of the bow quarters. F7 colours that
compartment, and a damage-control team sent there repairs the starboard motor.
`Zones.cfg` is global, so every class is affected. **Not caused by APE, and not stock
SH3.** APE only makes it common, because motor flooding damage always damages both
motors, and stock hits rarely reach the starboard one.

## Evidence (live, OAE lab, `sh3.exe` `20360fe8…`, Type IIA at 30 m, 2026-11-25)

The damage floats were written directly (`[[[[0x5543DC]+4]+0x37C]+0/4]`). F7's
schematic colours were read from screenshots. No zone damage, breach or flood value
changed in any run.

| Write (fresh mission each block) | Electric engine room | Bow quarters |
|---|---|---|
| port 0.1, 0.5, 0.9 alone | yellow, yellow, orange | white |
| then starboard 0.05 | orange | **yellow** |
| starboard 0.1, 0.5, 0.9 alone | **white** | yellow, yellow, orange |
| **with `[ElectricEngine1] Father=30`** (the one change), starboard 0.1, 0.5, 0.9 | yellow, yellow, orange | **white** |
| same, then port 0.9 | orange | white |

- An earlier reading, "both motors damaged trips it", was wrong: starboard alone does it.
- Writing a float back to 0 doesn't clear F7's colours: they latch until the game's own
  repair path runs. In the owner's run, once the crew had repaired both motors, both
  compartments went back to white.

Scripts, rows and screenshots are in the sh3-crush-pressure repo:
- `work/oae_battery_probe.py`, `work/oae_f7_colors.py`
- `work/oae-battery*.jsonl`
- `work/drive/20261125T135205Z/`, `work/drive/20261125T142016Z/`

The `Zones.cfg` edit was made in the lab copy through the lab ledger, then undone. The
lab file is byte-identical to the owner's again.

## Options (owner to decide)

1. **Patcher fix, optional and revertible:** set `[ElectricEngine1] Father=30` in
   `data/Zones.cfg`, keeping a pristine copy for revert, like ticket 16's `.zon` sites.
   It also changes stock damage routing: hits to the bow quarters would no longer damage
   the starboard motor. That is almost certainly what GWX intended.
2. **Document only:** a README note that on GWX-derived installs, starboard motor damage
   shows in the bow quarters and damage control must be sent there to repair it.
3. Both.

Checked 2026-11-25: GWX-KC 2.5 carries the same line (see the comment below). GWX
itself is not checked.

## Comments

### 2026-11-25: built, both options

- **Patcher fix**, "Starboard motor zone fix", optional, off until checked:
  - `src/patcher_gui/MotorZoneFix.cs` finds `[ElectricEngine1]`'s uncommented `Father`
    in `data/Zones.cfg`. It is offered only when that value is exactly 24.
  - On first apply it keeps `Zones.cfg.ape-orig` and changes those two bytes to `30`.
    Unchecking puts the file back byte for byte and deletes the copy.
  - It reads as **not needed** when the value is anything else, such as vanilla's 30.
  - A file changed by another mod after APE wrote it is left alone, and so is one
    without the line.
  - It goes through `PatchSession`'s usual verify-then-write, with full rollback, and
    is written after the `.zon` files and before `ape.act`.
  - The UI section sits under Depth stress. The Review describes it in game terms.
- **Tests:** `tests/patcher_gui/MotorZoneFixTests.cs`, 12 cases, both cultures. The
  full suite passes, 254. A one-off run on the real files found OAE 1.5.2's
  `Zones.cfg` faulty, with exactly 2 bytes changing (line 2477, `Father=24`), and
  vanilla's not needed.
- **README:** a note in the release `readme.html`, which renders to the v1.14 PDF, and in
  `README.md`. It also tells players without the fix to send repair teams to the bow
  quarters.
- **Not checked live:** whether the game re-reads `Zones.cfg` when a mission loads. The
  lab test relaunched the game, so the patcher says "the next time you start SH3".

### 2026-11-25: GWX-KC 2.5 has the same line; the fix stays in APE

- The owner's GWX-KC 2.5 install (`C:\games\GWX_KC_2.5\data\Zones.cfg`, the only
  `Zones.cfg` in that folder) has `[ElectricEngine1] Father=24` and
  `[ElectricEngine2] Father=30`, the same as OAE 1.5.2. So this is a GWX damage-model
  line, not an OAE-only one.
- The owner suggested moving the fix out of APE into an OAE JSGME mod, on the belief that
  GWX-KC was unaffected. That belief turned out wrong, so the fix stays in the patcher:
  it already turns up on any install whose value is 24, GWX-KC included, and it changes
  two bytes instead of replacing the whole file per mod.
- README.md and the release readme now name OAE and GWX-KC.
