# Diesel-damage patch: scope

Type: grilling
Status: open

## Blocked by

None. (Was `02`. The config half is now settled in principle — settings ship as
static bytes via the GUI patcher, not `ape.cfg`; see `03-hull-repair-patch-scope.md`.
Only the *delivery* details remain, in 05/06, and those don't gate the behavioral
questions below.)

## Question

Goal 3: patch the hsie diesel-damage mod so there's a chance of damage after
eight hours (of what — running time? mission time? needs pinning down).
Stretch goal: make damage amount, damage chance, and time-until-damage-chance
all settable via `ape.cfg`, using the config strategy decided in ticket 02
(shared ape.cfg strategy across subsystems).

Pin down enough to spin up its own `.scratch/diesel-damage-patch/` feature
effort (PRD + issues, same shape as `crush-depth-damage`):

- What "eight hours" measures (engine running time, mission clock, real
  time) and what timer/counter the existing hsie mod already exposes for
  this, if any.
- Damage-chance model: rolled once at the eight-hour mark, or a recurring
  chance-per-interval after eight hours? Affects both the base behavior and
  what "chance" means as a config key.
- Damage amount semantics: flat HP hit, rate, or band (cf. crush-depth
  damage's HP-per-game-second bands) — does this reuse `src/core/damage_band.*`
  or need its own model?
## RE status (updated 2026-08-15) — the site is already located

The "is this RE work?" bullet this ticket originally carried is **answered**. The
static-diff method (`docs/re-notes/03-utanker-hull-repair-signature.md`, §0–§2)
found the diesel-damage patch without a debugger, and corrected a wrong premise
along the way: h.sie's patches are **not** in `hsie.act` — they live in sections
appended to `sh3.exe` itself.

- **Enable flag:** VA `0x0063CF13` (file `0x0014EF13`), `0xFF` = on.
  `sh3_Diesel_Damage.exe` differs from `sh3_vanilla.exe` by that byte alone.
- **Cave:** `0x00635049`, gate at `0x00635075` (`cmp byte [0x63CF13], 0xFF`),
  entered from a `jmp 0x4D7195` trampoline near `0x00635040`.
- **Shape:** substantially larger than the hull-repair cave — it tracks a
  previous/current value pair (`[0x63E1D4]`/`[0x63E1D8]`), walks a two-element
  structure at `[0x5543DC]→+4→+0x30C`, calls an RNG helper (`0x6371A8`), scales
  by per-index constant tables at `0x63C0AC` and `0x63C0B8`, and compares against
  thresholds `0x63C0A4` (`0.0002`) and `0x63C0A8` (`1e-06`). A `0x3F800000`
  (`1.0`) comparison recurs throughout — likely an engine-health fraction.

So **expect this ticket to confirm and label an existing model rather than hunt
for one.** Remaining RE, to fold into the answer: which counter supplies "eight
hours", what the two-element structure is (port/starboard diesel?), and which
constant is the damage chance vs the damage amount. Note the diesel cave reads
the *same* singleton `[0x5543DC]` as the u-tanker mod's block 1, so identifying
that object serves both tickets.

Delivery is already decided in principle: static bytes written by the GUI patcher
(05), not a runtime `ape.cfg` read. Confirm the chance/amount/timer knobs are all
expressible as patchable operands — if any must vary with live game state, that
is a genuine exception worth raising against 05/06.

## Caveats inherited

- Verified against the sibling-build variant set in `data/exe_with_hsie_patches/`,
  not registered build `4B6BEE20`.
- VAs are absolute; ADR-0003 wants wildcarded AOB signatures with uniqueness
  proofs. Not yet done for this cave.
