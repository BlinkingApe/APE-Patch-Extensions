# APE Patch Extensions

APE is a small suite of patches for *Silent Hunter 3* with h.sie/Stiebler hardcode patches, adding new tunable behavior — currently a reworked u-tanker hull-repair rate, a rewritten diesel-engine damage model, depth stress, and one crush-time setting for every U-boat.

## Usage

![The APE GUI patcher, showing hull-repair and diesel-damage settings against a KC 2.5 install](images/APE%20v1.12.png)

- **The GUI patcher** (`src/patcher_gui/`) — a standalone Windows tool you
  drop in the SH3 game root. It finds your install, shows what's currently
  applied, lets you edit the hull-repair, diesel-damage, crush-time and
  depth-stress settings, and commits every change as a verified write to
  `sh3.exe`, `hsie.ini`, `data/Menu/en_menu.txt`, `data/Zones.cfg` and the
  U-boats' `.zon` files. Unchecking a feature and applying it again restores the original
  bytes exactly — that's the uninstaller.
- **Depth stress** (`ape.act`) — below 100 m, and more below the player's
  tier-2 depth, the electric engine room takes on water and hull damage is
  multiplied. While that room is flooded past a threshold and the motors are
  running, both motors take damage. The patcher deploys `ape.act` into the
  game folder with the player's values written in. Stock `sh3.exe` loads
  every `*.act` there at startup, so nothing else is installed.
- **Starboard motor zone fix** — OAE's damage model (from GWX) puts the
  starboard electric motor in the bow quarters (`data/Zones.cfg`,
  `[ElectricEngine1] Father=24`; stock SH3 has 30, the electric engine room).
  Its damage then shows in the bow quarters, and only a repair team sent
  there repairs it. This optional fix sets it back to 30, keeping the
  original as `Zones.cfg.ape-orig` for the revert. Without it, send your
  repair team to the bow quarters to fix the starboard motor.

Diesel damage builds on h.sie's own Diesel Damage switch, which is off by
default in OAE and GWX. If the patcher shows it as unavailable, turn the
switch on in `HsieOptionsSelector`, then reopen APE. APE reads h.sie's
switches but never changes them.

## Download

Pre-built patcher binaries are published under this repo's
[Releases](../../releases). Download the latest release archive, extract
the patcher exe into your SH3 install root alongside `sh3.exe`, and run it.

### Linux (Wine/Proton)

As of v1.13, APE runs under Wine's builtin `msvcr71.dll`. No DLL override
is needed.

If you applied diesel damage with v1.12 or earlier, open the v1.13 patcher
and press **Apply** once. Older versions found the C runtime's `pow` at a
fixed offset that only exists in Microsoft's own `msvcr71.dll`, so the game
crashed under Wine's version unless you set `WINEDLLOVERRIDES="msvcr71=n,b"`.
v1.13 looks `pow` up by name instead. Once you've re-applied, you can remove
that override.

## Building from source

Requires MSVC (Visual Studio Build Tools 2022 or later) with the **x86**
toolset, CMake 3.21+, Ninja, and .NET Framework 4.x tooling for the GUI
patcher.

```
cmake --preset x86-release           in a vcvars32 shell, then
cmake --build --preset x86-release   -> build\x86-release\ape.act (embedded by build.cmd)
src\patcher_gui\build.cmd            -> dist\ApePatchExtensions_v<ver>.exe
src\patcher_gui\build.cmd run        build and launch against the live install
tests\patcher_gui\run.cmd            run the patcher test suite
```

See [`src/patcher_gui/README.md`](src/patcher_gui/README.md) for the
patcher's internal shape and test conventions.

## Project layout

- `src/patcher_gui/` — the C# WinForms GUI patcher (the shipped tool).
- `src/`, `third_party/` — the C++ `ape.act` runtime module and its vendored
  dependencies (MinHook, doctest).
- `tools/re-static/` — Python static-analysis scripts used to derive and
  verify the patch manifest against real `sh3.exe` builds.
- `docs/adr/` — architecture decisions. `docs/re-notes/` — reverse-engineering
  notes behind each patch site. `CONTEXT.md` — project glossary and domain
  vocabulary.
- `.scratch/` — this project's issue tracker: PRDs and tickets as markdown,
  organized one directory per feature effort.

## Attribution

APE's patch data (offsets, replacement bytes, and derived formulas) is
reverse-engineered from **h.sie/Stiebler**'s hardcode patches. Credit
to both for the original patch logic APE builds on. APE does not modify or
redistribute `sh3.exe` itself — the patcher edits your own copy in place.

## License

APE's own code is licensed under the [MIT License](LICENSE). Vendored
third-party libraries under `third_party/` (MinHook, doctest) keep their own
licenses (both MIT).
