# Depth-pressure stress (APE)

Status: ready-for-agent

Supersedes the three-band crush-depth damage PRD (ADR-0008). Charted and decided on the
wayfinder map, `.scratch/depth-pressure-stress/map.md`. Vocabulary is `CONTEXT.md`'s:
**depth tier**, **hull multiplier**, **seepage**, **motor flooding damage**, **crush
depth**, **rated crush depth**, **crush speed**, **crush time**.

*Amended 2026-11-25 by ADR-0012 (feedback from Stoss): seepage goes into the electric
engine room on every class, leakage defaults are halved, and motor flooding damage
(keyed to that room's water and the telegraph) replaces random electric-engine wear.*

## Problem Statement

In OAE, a U-boat is untouched by depth until it crosses crush depth, and then stock's
crush damage takes over. Nothing happens in between. A boat at 220 m is as healthy as
one at 60 m, so deep evasion costs the player nothing until the moment it costs
everything.

That contradicts how the boats were built. Most classes, even the Type II, were
designed for a 100 m operating depth with a 2.5× safety margin, which was standard
German practice. Above 100 m there should be no trouble. Below it the hull is under
growing stress, and secondary systems suffer: cooling-water lines were only pressure-tight
to design depth, so bearings ran hot. Players and the owner want depth to be a continuum
of stress, not a cliff edge.

A second problem sits at the cliff edge itself. Stock crush speed differs by class and
by mod bundle (OAE ships 0.03–0.1 HP/s, KC 2.0), so how long a boat survives below crush
depth is arbitrary and can't be set in one place.

## Solution

Two patches in the APE GUI patcher.

**Crush time (built, ticket 16).** The player enters one crush time in seconds. The
patcher writes each playable U-boat class's `CrashSpeed = Hit_Points ÷ crush time` into
its `.zon`, so every class crushes on the same nominal scale. It is a static edit,
already built and committed, and can ship before anything else here.

**Depth stress (this PRD's new work).** Below 100 m the boat is in tier 1; below a
player-set tier-2 depth (default 150 m) it is in tier 2. Each tier carries two
effects:

- a **hull multiplier**: whatever hull damage the boat takes is amplified;
- **seepage** ("Leakage" in the GUI): water rises in the electric engine room, which
  the crew must pump.

A third effect follows from the water, not the depth:

- **motor flooding damage** ("Motor damage" in the GUI): while the electric engine
  room's flood is at or above the player's threshold and the telegraph isn't at stop,
  both motors take damage, never past the point where the crew can still repair it. It
  acts at any depth, whatever flooded the room.

Below crush depth (tier 3) stock's crush damage runs at the crush speed the crush-time
patch set. APE's tier effects continue at tier-2 values, except that crush damage itself
is never multiplied.

At runtime this is `ape.act`, APE's injected module, which reads and writes game memory
from its own thread and never hooks a stock function (ADR-0001). The patcher deploys
`ape.act` with the player's numbers baked in, and deletes it when the feature is
unchecked (ADR-0006).

## User Stories

### Player: depth as stress

1. As an OAE player, I want no depth effects at all above 100 m, so that normal operating depth stays free of penalty, as it was for the real boats.
2. As an OAE player, I want depth stress to begin at 100 m whatever my boat's class, so that the threshold matches the historical design depth rather than a per-class number.
3. As an OAE player, I want a deeper, harsher second tier from a depth I choose (default 150 m), so that going further down is clearly riskier than just passing 100 m.
4. As an OAE player, I want any hull damage I take in a tier to hurt more (×1.25 in tier 1, ×1.5 in tier 2 by default), so that being depth-charged deep is worse than being depth-charged shallow.
5. As an OAE player, I want water to seep into my electric engine room while I'm in a tier, so that depth has a running cost my crew has to manage.
6. As an OAE player, I want tier-1 seepage to be a nuisance any crew can pump (2 %/min against a fresh crew's ~19.6 %/min), so that a long patrol at 120 m is tolerable.
7. As an OAE player, I want tier-2 seepage to fill an unmanned room fast but be held by any crew (7.5 %/min by default, against ~14.6 %/min tired), so that staying deep punishes leaving the room unmanned.
8. As an OAE player, I want seepage never to fill the compartment past 0.8, so that depth stress alone never drowns the room's crew and sinks me.
9. As an OAE player, I want seepage to stop rising when I come up above 100 m, so that ascending is my way out.
10. As an OAE player, I want water APE added to stay until my crew pumps it, so that it behaves like real water rather than vanishing on ascent.
11. As an OAE player, I want an uncrewed electric engine room to keep filling (to the 0.8 cap), so that crewing it is my lever and the tooltip tells me so.
12. As an OAE player of any class, I want seepage in the electric engine room, where the motors and shaft glands are, so that the water and the motors share one room and one crewing decision (ADR-0012).
13. As an OAE player, I accept that unpumped seepage cuts my electric power (stock's cut starts at 0.2 flood and is total at 0.7, below the 0.8 cap), so that losing the motors is the price of leaving the room unmanned.
14. As an OAE player, I want both motors to take damage while their room's water is at or above my flooding threshold (30 % by default) and the telegraph isn't at stop (10 % per game-second by default, provisional), so that the motors suffer from water, as the historian and Stoss describe.
15. As an OAE player, I want motor flooding damage never to push an engine past 0.9 damage, so that my crew can always repair it at sea.
16. As an OAE player, I want ordering all stop, or pumping the room below the threshold, to stop the damage, so that I can put the motors out of service to save them.
17. As an OAE player, I want motor flooding damage to act at any depth, whatever flooded the room (APE's seepage or a breach), so that ascending with a flooded motor room doesn't make running the motors safe.
18. As an OAE player, I want APE never to repair or lower anything stock damaged, so that APE only adds stress and never interferes with stock damage.
19. As an OAE player, I want the tier effects to keep acting below crush depth, at tier-2 values, so that crossing crush depth doesn't switch the stress off.
20. As an OAE player, I want crush damage itself never to be multiplied, so that the crush time I chose stays what it says.
21. As an OAE player, I want a depth charge that lands while I'm crushing to still be multiplied, so that only crush damage is exempt.
22. As an OAE player, I want tier 3 to start wherever stock's crush damage starts, even above my tier-2 depth when my hull is damaged, so that APE's tiers follow stock's own onset.
23. As an OAE player, I want time compression and pause to scale every effect with game time, so that 10 minutes at 8× costs what 10 game-minutes should, and a paused game costs nothing.
24. As an OAE player, I want loading a saved, damaged boat to cost me no extra hull, so that the multiplier never mistakes the save restore for damage.
25. As an OAE player, I want APE to do nothing, rather than guess, if it can't find or trust a value in game memory, so that a mismatch with my install never wrecks my boat.

### Player: the patcher

26. As an OAE player, I want a "Depth stress" section in the GUI patcher, so that I set it up the same way as hull repair, diesel damage and crush time.
27. As an OAE player, I want to set the tier-2 depth, per tier the leakage and hull multiplier, and once the flooding threshold and motor damage amount, so that I can tune how harsh depth is.
28. As an OAE player, I want each effect to switch off at its neutral value (leakage 0, motor damage 0, multiplier 1.0), so that I can keep some effects and drop others without extra checkboxes.
29. As an OAE player, I want each field checked only against its own range, so that I'm free to make tier 2 milder than tier 1 if I want.
30. As an OAE player, I want units in game terms (metres, % per game-minute for leakage, % flood for the threshold, % per game-second for motor damage, ×), so that I don't have to think in raw floats.
31. As an OAE player, I want the tier-2 depth tooltip to say that a class whose crush depth is shallower goes straight from tier 1 to tier 3, so that the IIB and XXIII don't surprise me.
32. As an OAE player, I want the leakage tooltip to say the electric engine room must be crewed to be pumped, and the motor damage tooltip to say that ordering stop protects the motors, so that I know my levers.
33. As an OAE player, I want the review panel to describe what depth stress will do in game terms before I apply, so that I know what I'm committing to.
34. As an OAE player, I want the patcher to show my current depth-stress values when I reopen it, so that it reads back what's installed.
35. As an OAE player, I want unchecking depth stress and applying to remove `ape.act` entirely, so that revert is the same one step as every other APE feature.
36. As an OAE player, I want no `ape.act` in my game folder unless I've enabled a runtime feature, so that APE installs nothing it doesn't need.
37. As an OAE player, I want depth-stress settings to take effect at the next game launch, and the patcher to say so, so that I don't expect a running game to change.
38. As an OAE player, I want the crush-time patch to stay usable on its own, so that I can set crush time without running `ape.act`.

### Maintainer

39. As an APE maintainer, I want all of the effect logic in one pure step function, so that tiers, the multiplier, the exemption, the guards, the ceilings and the motor-damage trigger are unit-tested without a running game.
40. As an APE maintainer, I want each live value reached through an AOB-anchored root and a recorded pointer chain, so that a bundle update that moves an anchor is detected and fails inert rather than misread (ADR-0003).
41. As an APE maintainer, I want each live value checked against a plausibility gate before use, so that a stale chain reading garbage writes nothing.
42. As an APE maintainer, I want `ape.act` to verify its constants struct's marker at startup and fail inert on mismatch, so that an unpatched template or a corrupted deploy never runs with garbage.
43. As an APE maintainer, I want the constants in a magic-marked struct at a build-recorded offset in the manifest, so that the patcher writes and reads them the way it already handles other sites (ADR-0006).
44. As an APE maintainer, I want motor flooding damage deterministic (`amount · dt`), so that no generator or `rand()` trap (ticket 12) is involved and it scales exactly with time compression.
45. As an APE maintainer, I want a log line for every poll that writes, plus every fail-inert skip with its reason, so that I can cross-check in-play behaviour against the arithmetic.
46. As an APE maintainer, I want the seep compartment chosen at run time from the zone→compartment table, never by class, so that U-boats added by mods are covered too.
47. As an APE maintainer, I want the hard ceilings (seep 0.8, motor damage 0.9) fixed in `ape.act` and not exposed, so that the repairability and survival guarantees hold however a player sets the fields.
48. As an APE maintainer, I want every addressed value documented with its AOB, chain and the OAE build hash it was proven on, so that I can tell whether it still holds after a bundle update.
49. As an APE maintainer, I want the five in-play acceptance tests from ticket 14 run against the built `ape.act`, so that the by-construction claims are proven against the real game.

## Implementation Decisions

### Modules

- **`ape.act` (C++, PE32/x86).** Four parts:
  - **Loader.** `DllMain` reads and verifies the constants struct, then starts one
    background loop thread and nothing else. No work happens under the loader lock and
    no function is hooked (ADR-0001).
  - **Game-memory reader.** Resolves the anchors, follows the chains, gates
    plausibility, and produces one **sample** per poll, or a reason it couldn't.
  - **Depth-stress step**, the pure seam below.
  - **Writer.** Applies the step's writes to the addresses the reader resolved in the
    same poll.

  The loop polls at about 10 Hz. The poll rate is a free choice: all arithmetic runs on
  Δ(mission clock), never wall time.
- **Constants struct (ADR-0006).** One struct for depth stress, in `ape.act`'s own
  `.data`, magic-marked, with a version field. It holds the tier-2 depth, per tier
  the seep rate and hull multiplier, and the flooding threshold and motor damage
  amount, all float32. (Changing the field set bumps the version.) The ceilings
  are not in it. The build records the struct's file offset in the generated manifest.
- **GUI patcher (C#).** A new `ape.act` store alongside `sh3.exe`, `hsie.ini`,
  `en_menu.txt` and `.zon`. The C++ build's `ape.act` is embedded in the patcher exe as
  a template. On apply with depth stress enabled, the patcher copies the template to the
  game root, verifies the marker at the manifest offset, and writes the struct. On
  apply with it disabled, it deletes `ape.act`. Read-back comes from the deployed file
  if there is one, otherwise from the embedded template (ADR-0006). The write joins
  `PatchSession`'s all-or-nothing apply and its rollback.
- **`PatchState`** gains a depth-stress feature flag and the seven fields. **`Review`**
  gains a depth-stress group, in game terms. **The UI** gains a "Depth stress" section
  with the tooltips named in the user stories.
- **One feature checkbox** for depth stress, as for every other APE feature. Unchecking
  it is the revert. Individual effects switch off at their neutral values, with no
  per-effect checkboxes (ticket 12).

### Fields (ticket 12, amended by ADR-0012; the owner may retune after testing)

| Field | Unit shown | Range | Neutral | Tier 1 | Tier 2 |
|---|---|---|---|---|---|
| Tier-2 depth | m | 101–300 | — | from 100 m, fixed | **150** |
| Leakage | % of the electric engine room per game-minute | 0–60 | 0 | **2** | **7.5** |
| Hull multiplier | × | 1.0–5.0 | 1.0 | **1.25** | **1.5** |
| Flooding threshold (one, not per tier) | % flood of the electric engine room | 5–95 | (damage 0) | **30** | |
| Motor damage (one, not per tier) | % of each motor's health per game-second | 0–100 | 0 | **10** (provisional) | |
| Crush time (built) | s, whole | 1–3600 | — | 5 | |

The threshold and motor damage defaults are provisional: Stoss will recommend values.
Not exposed, fixed in `ape.act`: **seep ceiling 0.8**, **motor damage ceiling 0.9**. No
cross-field validation. Conversions happen in the patcher or at load, never per poll:
leakage `p` %/min is `p/6000` flood per game-second; threshold `t` % is `t/100` flood;
motor damage `a` %/s is `a/100` damage per game-second.

### The sample (what the reader hands the step)

Every value below is proven on OAE (`sh3.exe` sha256 `20360fe8…`). Tickets 08–11, 17
and 19 hold the evidence; `docs/re-notes/08-oae-port-of-depth-leads.md` holds the port.

| Value | Where | Type |
|---|---|---|
| Gauge depth D | `[[["sh3.exe"+0x2307BC]+0x4]+0x4]+0x54` | float, m |
| Mission clock | `0x0063FF80` (h.sie's mirror; reads 0 whenever stock's clock does) | int32, game-s |
| Boat object | `[[[[[0x00554A88]+0xC4]+0x2240]+0x24]+0x4C]`, the pointer itself being the boat's identity | pointer |
| Hull, max hull | boat `+0xB8`, `+0xB4` | float, HP |
| Rated crush field `CD` | boat `+0xC0` = `.zon` CrashDepth ÷ 10 | float, tens of m |
| Crush speed `CS` | boat `+0xC4` = `.zon` CrashSpeed after load | float, HP/s at the reference |
| Electric engine room flood (seepage and the motor-damage trigger) | zone list at `0x005547D4`: walk `node = [node+0x4]`; zone is `[[[node+0x14]+0x10]+0x8]`; flood is float `[node+0x14]+0x1C`, 0 dry → 1 full. Compartment from the zone→compartment table `0x00566898`: the zone mapping to compartment 4, on every class (ADR-0012). | float |
| Engine damage, port/starboard | `[[[[0x005543DC]+0x4]+0x37C]+0x0]` / `…+0x4]`, each a pointer to the engine's float | float, 0 → 1 |
| Ordered machine telegraph | `[0x0063E1D4]`, h.sie's diesel cave copy, refreshed per tick (`docs/re-notes/04-diesel-damage-signature.md` §4). 0.0 = stop. **Not yet proven on OAE while submerged on electric, and astern's encoding is unconfirmed** (ticket 30). | float |

- **Anchors.** Recorded AOBs, each unique on OAE: the zone-list head
  `8B 3D D4 47 55 00 85 FF C7 44 24 40 00 00 00 00`; the engine root
  `89 35 DC 43 55 00 57 8D 7E 10`; and the engine-block offset
  `8B 80 7C 03 00 00 8D 77 FC`. Three roots have proven static addresses but no
  recorded signature: the boat root `0x00554A88`, the depth root and the clock.
  Deriving a signature from an instruction that references each address is
  implementation work, not a decision. Prefer a stock instruction to an h.sie cave
  (the only recorded reference to `0x554A88` is h.sie's cave at `0x00633633`). The
  signatures go in `docs/re-notes/`.
- **Plausibility gates.** Hull is in [0, max]; max is > 0; D is in [0, 1000); CD > 1;
  CS ≥ 0; floods and damages are in [0, 1]; the clock is ≥ 0. A failed gate, an
  unresolved chain or a failed read means **no sample**.
- Avoid the HUD hull mirror `0x566A28` and the crew report's float mirror at
  `0x005FD860`. Both are copies the engine overwrites.

### The depth-stress step (the pure seam)

Inputs: the constants, the previous **step state**, and either a sample or "no sample".
Outputs: the new step state and a list of writes
(hull, flood, port or starboard engine), each with its new value.

1. **No sample:** no writes, and the state is cleared so the next sample re-baselines.
2. **Baseline.** The state holds the previous boat identity, hull, clock and
   "previous clock was 0". Re-baseline with no writes when there is no previous state
   or the boat identity changed. Re-baseline the hull, with no amplify, if hull rose.
3. **dt** = clock − previous clock, in whole game-seconds. If dt ≤ 0 (paused, or the
   clock went back), there is no seepage and no motor damage this poll.
4. **Tier.** With `h = hull/max`, `E = 1 + (CD − 1)·h` and the onset at `7·E` m
   (that is, `0.7·E` in tens of metres): tier 3 if D ≥ onset; else tier 2 if D ≥ the
   tier-2 depth; else tier 1 if D ≥ 100; else none. Tier 3 uses tier-2 values. The tier
   comes from the current sample.
5. **Hull multiplier.** N = previous hull − hull. If N > 0, the tier's mult > 1, and the
   previous clock wasn't 0 (save-load guard 2): in tier 3 the exempt share is
   `X = CS · dt · (D/10 − 0.7E)/(0.3E)`, floored at 0, and 0 outside tier 3. Write
   `hull − (mult − 1) · max(0, N − X)`, floored at 0. D is the gauge depth with no
   reference offset: the gauge reads 2.6–5 m deeper than stock's reference point, so the
   error only ever exempts more (ADR-0011). The new baseline is the hull after APE's
   write.
6. **Seepage.** In a tier, with leakage > 0 and dt > 0: if flood ≥ 0.8, no write; else
   write `min(flood + rate·dt, 0.8)`. Never lower it.
7. **Motor flooding damage.** At any depth, tier or none, with amount > 0 and dt > 0:
   if the electric engine room's flood (as read this poll, before APE's seepage write)
   is ≥ the threshold and the telegraph is not 0.0, then for **each** engine: if its
   damage is ≥ 0.9, no write; else write `min(d + amount·dt, 0.9)`. Never lower it.
   No telegraph value (unresolved or implausible) means no motor damage this poll; the
   other effects still run.
8. **Fallback guard.** If live play shows guard 2 missing a restore, hold the baseline
   for ~2 game-seconds after a boat-identity change. Guard 1 plus guard 2 is the
   specified behaviour.

The 0.9 ceiling stores as 0.89999998 in float32, so the clamp can't round past it.

### Concurrency and failure

- APE's writes race the game's own per-frame writer: a plain read-modify-write on an
  aligned float, accepted (ticket 09: stock's damage is one `fsub` per collision, so a
  drop is never lost, only seen one poll later).
- **Fail inert everywhere.** A marker mismatch means the loop never starts. No sample
  means a skipped poll with a logged reason. Nothing is ever guessed.
- **Logging** to the game root's `ape_act.log`: startup (marker and constants), each
  poll that writes (D, tier, onset, dt, N, X, each write's old → new), and each skipped
  poll's reason, rate-limited.

### Crush time (built; recorded here for completeness)

`CrashSpeed = Hit_Points ÷ T`, written to the `.zon` of every class whose unit `.cfg`
has `[Unit] HumanPlayable=YES`. The revert copy is `.zon.ape-orig`, and drift detection
is as built. Its effect is nominal (ADR-0011): from full hull a boat lasts about 1.3·T
just past the onset and 0.3·T at its rated crush depth. It takes effect at the next
mission load. AI boats of a patched class change too.

## Testing Decisions

- **Good tests here assert external behaviour only**: given a sample sequence, which
  writes come out; given an install, which bytes and files come out. They never reach
  into private state and never assert on log text or control positions.
- **Seam 1, the depth-stress step (C++, vendored doctest).** Feed sequences of samples
  and assert on the writes. Cases:
  - Tier boundaries: just above and below 100 m, the tier-2 depth and the onset.
    Tier 3 lifted above the tier-2 depth and above 100 m by a damaged hull.
  - Multiplier at each tier, at neutral 1.0, with N ≤ 0.
  - Ramp-share exemption: pure crush gives no extra; crush plus a hit multiplies only
    the hit; X is floored at 0.
  - Save-load guard 1 (identity change re-baselines); guard 2 (a drop after a clock-0
    poll isn't amplified); a hull rise re-baselines.
  - Pause (dt 0) and clock regression: no seep or motor damage.
  - Seep: steps by `rate·dt`, clamps at 0.8, makes no write at or above 0.8, never
    lowers.
  - Motor damage: both engines step by `amount·dt`; clamps at 0.9 (allowing for the
    0.89999998 store); no write at or above 0.9; never lowers; nothing below the
    threshold, at telegraph 0.0 or with no telegraph value; acts above 100 m when the
    room is flooded; astern counts as running.
  - No sample: no writes, and the next sample re-baselines.
  - Neutral values switch each effect off independently.
- **Seam 2, `PatchSession` (C#, the existing suite).** Prior art: `CrushTimeTests` and
  `PatchSessionTests`. Cases: deploy on enable, with the marker verified and the struct
  bytes written; read-back from the deployed file and from the template; revert deletes
  `ape.act`; a wrong marker or offset refuses the apply; rollback on write failure;
  ranges and neutral values; the Review wording. Both cultures, as the suite already
  runs.
- **Outside both seams, verified in play only** (ticket 14's list, against the built
  `ape.act` on OAE, hashing `sh3.exe` first):
  1. A depth-charge or ramming hit at tier-2 depth removes `mult ×` the stock hull
     delta, while crush damage below crush depth is not multiplied.
  2. An undamaged, unattacked boat at tier-1 depth takes water in the electric engine
     room, on a VII or IX as well as a II. The water stops rising above 100 m and isn't
     removed by APE. Stock's power cut follows the water on the VII/IX too.
  3. With the room past the threshold and the telegraph ahead, both motors take damage,
     never past 0.9; ordering stop ends it, as does pumping below the threshold; it
     continues above 100 m while the room is flooded; the crew repairs from 0.9.
  4. All effects keep acting below crush depth.
  5. Loading a damaged boat costs no amplified hull.

  Also, unrun from ticket 16 and kept as a smoke test: crush-time timings for a VIIC
  and a IIA.

## Out of Scope

- **The `sh3.exe` load call site for `ape.act`** (ADR-0002, old ticket 02, and the
  umbrella map's open question of whether the call site tolerates a missing file). It is
  a hard prerequisite: the patcher deploys `ape.act`, but nothing loads it until that
  site exists. It belongs to the umbrella map, as does which APE release ships this.
  Until then the depth-stress section is shown disabled, with an explanation.
- Replacing stock's crush mechanism. APE sets its speed (crush time) and reads its
  onset, nothing more.
- Amplifying anything but hull HP. Depth-charge flooding and equipment damage stay at
  stock magnitude. (Motor flooding damage does add to the consequences of stock breach
  flooding in the motor room; it is a new effect, not an amplification. ADR-0012.)
- Damage from depth on systems beyond the electric engines. The zone-state tree is walkable (ticket
  11), so this is a future effort.
- AI boats. Player boat only; AI boats are affected only through the shared `.zon`
  crush speed.
- Runtime support beyond the proven OAE build. KC and other bundles get whatever the AOB
  scan and the gates allow, which is fail-inert if anything moved.
- Hot reload of constants; a config file of any kind (ADR-0006).
- Per-effect enable checkboxes and cross-field validation.

## Further Notes

- **Superseded work.** Old ticket 05 (`ape.act` wiring for the dead band model) is
  replaced by the implementation issues spun up from this PRD. Old ticket 01 is kept for
  its static-pass write-up only.
- **Cause attribution is impossible by design** (ADR-0008). A multiplier firing on
  grounding at 120 m is correct behaviour.
- **On every class, seepage cuts electric power** above 0.2 flood, total at 0.7, below
  the 0.8 cap. Accepted (ticket 19 for II/XXI, ADR-0012 for all).
- **OAE repairs 60× slower** (h.sie's Realistic Repair Times): ~0.19 damage per
  game-hour, ~4.6 game-hours from 0.9. At the provisional 10 %/s, a motor reaches 0.9
  about 9 game-seconds after the room crosses the threshold with the telegraph ahead.
- **Every OAE single mission spawned a Type IIA** in the lab. Compartment 4 on a VII or
  IX, and stock's power cut there, are from data, not live on OAE. In-play test 2 should
  be run on a VII or IX from a career if possible.
- Decisions and evidence: the map's Decisions-so-far and ADRs 0001, 0003, 0006, 0008,
  0010, 0011 and 0012.
