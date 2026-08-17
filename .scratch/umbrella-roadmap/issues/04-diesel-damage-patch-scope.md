# Diesel-damage patch: scope

Type: grilling
Status: resolved

## ⚠ Reopened 2026-08-15 — new premise from the user

After speaking with a historian, the user has a clearer and **much simpler**
picture of what this mod should do:

> A very low chance of damage **anytime the diesels are in use, at any speed** —
> low enough that a normal 13-patrol career (~10,000 hours of diesel operation)
> sees damage perhaps **once or twice**.

This supersedes the eight-hour-overload-gate resolution recorded under
`## Answer` below, which is kept for the record but is **no longer the decision**.
The grilling is live again; see `## Answer (2nd pass)` once it resolves.

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

## ⚠ The patch is disabled in every install surveyed (2026-08-15)

Found while resolving ticket 05. Flag `0x63CF13` (entry #20, "Diesel Damage Fix")
is `0x00` — **off** — in all four installs on the dev machine: OAE 1.5.2,
OAE 1.5.3.7, KC2.5, and the GWX folder. See `docs/re-notes/oae-builds.md`.

This bears on the ticket directly:

- **Confirm the user actually wants this patch running at all.** The whole goal is
  "patch the h.sie diesel-damage mod so there's a chance of damage after eight
  hours" — but nobody surveyed is currently running it. Establish whether it is
  off deliberately (disliked behaviour) or incidentally (never enabled). If the
  former, the goal may be to *replace* it rather than tune it.
- APE's patcher will show this feature **disabled with an explanation** until the
  user enables the h.sie patch in `HsieOptionsSelector` (ticket 05, decision A-2).
- Any RE done against `data/exe_with_hsie_patches/sh3_Diesel_Damage.exe` is still
  valid — that variant has the flag on — but it does not reflect any live install.

## Answer (2nd pass) — resolved 2026-08-15, supersedes the 1st pass below

The historian's picture replaces an *overload* mechanic with a *wear* one, and
the model collapses to a single constant.

### The model

Every term in h.sie's formula is flattened out of existence:

```
p_ds = p_dso x Factor x MTS^Exponent
     = p_dso x 1.0    x MTS^0        = p_dso     (a constant)
```

One Bernoulli trial per **game**-second, invariant in speed and in damage state,
whenever the diesels are running. No accumulator, no timer, no memory.

| Decision | Value | Why |
|---|---|---|
| Speed dependence | **none** — Exponent `0.0` for all three counts | "at any speed", read literally |
| Damage-state cascade | **none** — Factor `1.0` for all three counts | events are independent; "once or twice" means unrelated incidents, not a spiral |
| Stopped boat (telegraph 0) | **counts as in use** | surfaced diesels run to charge batteries; `pow(0,0)=1` gives this for free |
| Rate | **1.5 events / 10,000 h** → `p_dso = 4.1667e-8` | the user's stated target, reachable once `0x635818` is patched |
| Damage per event | **`+= rand(0.15 … 0.55)`**, single band | "should be repairable by the crew" — see below |
| Escalated second band | **removed** | h.sie's `rand(0.30…1.00)` destroys outright ~43% of the time |
| User tuning | **rate and damage band are GUI input fields** | the user wants to sweep values while testing |

### ⚠ A float32 trap in `0x635818` — found, then patched out

The single most important finding of this pass. `0x635818`, which charges time
compression as a trial count, does this:

```
00635821  fld1                   ; 1.0            (80-bit)
00635823  fsub  dword [ebp+8]    ; 1 - p          (80-bit)
00635829  fstp  dword [esp]      ; <-- ROUNDED TO FLOAT32
0063582C  call  0x6371a8         ; pow(1-p, TC)
```

Float32 spacing just below 1.0 is exactly 2⁻²⁴ ≈ 5.96e-8, so `(1-p)` lands on a
lattice and `p` is quantized to multiples of 2⁻²⁴ **whenever time compression is
above 1×** — i.e. during essentially all play:

| `p_dso` set in `hsie.ini` | `p` actually used at TC > 1 | Events / 10,000 h |
|---|---|---|
| < 2.98e-8 | **0.0** | **0 — silently inert** |
| 2.98e-8 … 8.94e-8 | 5.96e-8 | **2.15** |
| 8.94e-8 … 1.49e-7 | 1.19e-7 | 4.29 |

This makes the constants-only route floor out at **2.15 events / 10,000 h**, with
the next step down being zero and a **silent inert cliff at `2.98e-8`** — a mod
that stops working with no error and no in-game symptom.

**Resolution: replace `0x635818` rather than live on the lattice.** For `p` this
small the binomial is pointless — `1-(1-p)^TC ≈ p × TC` to better than 0.01% even
at 4096× — so the whole function becomes:

```
00635818  55           push    ebp
00635819  8bec         mov     ebp, esp
0063581B  d94508       fld     dword [ebp+8]      ; p
0063581E  d84d0c       fmul    dword [ebp+0xc]    ; p * TC
00635821  d9e8         fld1                       ; \
00635823  dbf1         fcomi   st(1)              ;  | min(1.0, p*TC)
00635825  dbc1         fcmovnb st(0), st(1)       ;  |
00635827  ddd9         fstp    st(1)              ; /
00635829  c9           leave
0063582A  c20800       ret     8
```

**21 bytes into a 38-byte body**, `int3`-padded to `0x635844`, `.pcode` and
therefore outside the `hsie.ini` clobber. The clamp is copied verbatim from
h.sie's own damage clamp at `0x6351D8` — and it is **required**, not defensive:
`p × TC` reaches 1.0 (damage every tick) once `p ≥ 1/TC`, which at 4096× is
`2.44e-4` — barely above h.sie's own shipped `p_dso` of `2e-4`, and well within
reach of anyone sweeping values in the GUI.

With this in place the lattice, the cliff, and the `TC == 1` / `TC > 1`
disagreement all disappear, and **the rate becomes continuously tunable** — which
is what makes a GUI input field honest rather than a lie about precision.

> The decision recorded earlier in this pass — "take the grid point, 2.15/career,
> ship no `.pcode` edit" — is **reversed**. Its premise died when the damage-band
> decision below put APE into `.pcode` anyway; at that point the rate fix costs
> one more site and buys back the user's actual target.

### Damage amount: one band, `rand(0.15 … 0.55)`

The historian's constraint is that damage **should be repairable by the crew on
the boat**, i.e. it must never reach `1.0` (destroyed). h.sie ships two bands,
selected at `0x6351BA` on `ebx` — the count of *currently* damaged diesels, a
**state, not an event counter** — and targeting at `0x6351A4` sends each hit to
the **more** damaged engine:

| h.sie's band | Mean | Rating |
|---|---|---|
| `0.01 … 0.59` (first hit) | 0.30 | Never destroys from pristine, so it passes. But a **59× spread**: a quarter of hits land ≤0.16 and are barely felt, a quarter land ≥0.44. For a once-a-career event, the whole experience is a coin toss. |
| `0.30 … 1.00` (while a diesel is hurt) | 0.65 | **Fails the constraint.** Additive onto the already-damaged engine: from a prior of 0.30 it destroys outright **43%** of the time; from 0.50, **71%**. It is h.sie's cascade finisher, built to punish continued flank running — which the flat model no longer has. It would fire only because an earlier, unrelated incident had not been repaired. |

**Decision: a single band of `0.15 … 0.55` for every hit.**

- mean **0.35**, spread cut from 59× to **3.7×**
- minimum 0.15 — every incident is a real fault, none are "barely felt"
- maximum 0.55 — one hit can never destroy
- two *unrepaired* hits reach 1.0 only **3.1%** of the time (h.sie's: 4.9%)

**Implemented as four data writes, not a control-flow edit.** All four immediates
are set to the same band, so the `cmp ebx, 0` / `je` / `mov` block still runs and
simply writes the values it already holds. The one-byte alternative
(`0x6351BD`: `74`→`eb`, `je`→`jmp`) is behaviourally identical but changes an
instruction; writing operands instead keeps **every site a verify-context +
mutable-field pair**, which is exactly ticket 02's manifest model.

| Site | VA | File offset | h.sie | APE |
|---|---|---|---|---|
| first-hit **hi** | `0x6351B1` | `0x1471B1` | 0.59 | **0.55** = `cd cc 0c 3f` |
| first-hit **lo** | `0x6351B6` | `0x1471B6` | 0.01 | **0.15** = `9a 99 19 3e` |
| escalated **hi** | `0x6351C3` | `0x1471C3` | 1.00 | **0.55** = `cd cc 0c 3f` |
| escalated **lo** | `0x6351CA` | `0x1471CA` | 0.30 | **0.15** = `9a 99 19 3e` |

Keeping the escalated pair addressable (rather than jumping over it) leaves the
door open to re-splitting the two bands later without a second code edit.

### Delivery: two write targets, both driven by APE's GUI patcher

**1 — `hsie.ini`, edited in place.** All eight diesel parameters are plain text
keys (lines 137–151), and the file already uses scientific notation
(`Val9=2e-4`, `Val10=1e-6`):

```ini
Val9  = 4.1667e-8      ; p_dso        (was 2e-4)  <- GUI field
Val10 = 1e-9           ; threshold    (was 1e-6) — must be < p_dso or inert
Val11 = 1.0            ; Factor,   0 damaged   (unchanged)
Val12 = 1.0            ; Factor,   1 damaged   (was 4.0)
Val13 = 1.0            ; Factor,   2 damaged   (was 8.0)
Val14 = 0.0            ; Exponent, 0 damaged   (was 200.0)
Val15 = 0.0            ; Exponent, 1 damaged   (was 20.0)
Val16 = 0.0            ; Exponent, 2 damaged   (was 10.0)
```

This **inverts ticket 09**: the `hsie.ini` runtime clobber stops being an
obstacle and becomes the delivery vehicle. Constraints on the implementation:

- **Edit the eight keys in place. Never ship a replacement file.** OAE has heavily
  customised the other `Val`s (torpedo failure, weather, snorkelling, collision) —
  an ini writer that preserves comments, ordering, and untouched keys is required.
- **`hsie.ini` becomes a third store**, alongside `sh3.exe` and `ape.act`. Ticket
  02's "the binary is the only store" does not hold for this feature. Read-back to
  populate the GUI works the same way, and arguably better — it is human-readable.
- h.sie files these under *"OTHER PARAMETERS (Changing not supported and very
  risky)"*, below a `NEVER DO ANY CHANGES BELOW THIS LINE` banner. That is his
  warning to users, not a technical barrier — but it is his stated intent, and
  APE's UI should say what it is doing.

**2 — `sh3.exe` `.pcode`**, five sites: the four damage-band immediates and the
`0x635818` rewrite. All outside the `hsie.ini` clobber list, so static writes
stick and verify-before-write is meaningful.

**Still no `ape.act` code and no runtime writes.** But note this feature now spans
**two write targets by itself** (`hsie.ini` + `sh3.exe`), which bears on ticket
07's "must v1 exercise both targets" question.

### GUI: the rate and the damage band are user input fields

The user asked for these explicitly, to sweep values while testing. This is only
honest *because* `0x635818` is patched — on the lattice, a free-text rate field
would have been a lie about the precision available.

- **Rate** — express as **events per 10,000 h** (default `1.5`), converted to
  `p_dso` internally. The raw probability is meaningless to a player.
- **Damage band** — `lo` / `hi` pair, default `0.15` / `0.55`. Validate
  `0 < lo < hi < 1`.
- **Show the derived consequences live**, since they are not obvious from the
  inputs: mean damage, and the "two unrepaired hits reach 1.0" percentage. A `hi`
  above 0.50 makes accumulation possible and the UI should say so.
- **`Val10` (threshold) is not exposed.** APE pins it below any reachable `p_dso`
  and hides it; its only purpose in h.sie's design was the low-speed cut-off that
  the flat model removes. Setting the rate to zero still turns the feature off
  cleanly.
- ⚠ **The tunables split across both write targets** — rate lands in `hsie.ini`,
  damage band in `sh3.exe`. One feature panel, two stores, and read-back for the
  GUI has to come from both.

### What this dissolves

- **The eight-hour accumulator, and with it the save/reload persistence problem** —
  which the map carried as the effort's hardest unsolved item. Memoryless was the
  objection to h.sie's model; under a per-hour wear rate it is the correct shape.
- **Ticket 10 (APE writing h.sie's flag table at runtime)** — its entire premise
  was this ticket's gate mechanism. APE never writes `0x63CF13`. Ticket 05's rule
  ("APE reads the flag table, never writes it") stands unmodified. Closed as moot.
- **The `[0x63E1D4]` telegraph anchor as a required lever.** Still a true and
  useful finding, but this feature no longer reads it.

### What it leaves

- **⚠ The surfaced gate is now load-bearing, and it is unproven.** The test at
  `0x6350CA` (`[0x5543DC+0x18] + [+0x1C] >= 0`) is *guessed* to mean surfaced.
  Under h.sie's `MTS^200` model an error there was nearly invisible; under a flat
  always-on rate, a wrong reading means diesels take damage while submerged on
  electrics. **This is the one finding that could invalidate the design.** Raised
  as ticket 11.
- **The enable flag is still `0x00` in every surveyed install.** Unchanged from the
  1st pass: the user ships with it on, and APE shows the feature disabled with an
  explanation until it is enabled in `HsieOptionsSelector` (ticket 05, A-2).
- **Repairability is assumed, not proven.** The whole damage-band decision rests
  on "below 1.0 the crew can fix it, at 1.0 they cannot". That is the standard SH3
  behaviour and it is supported by h.sie's own messaging — string 4893 fires on
  partial damage only, never on destruction — but **it has not been verified for
  this field**. Folded into ticket 11's live session. If repair does *not* clear
  the field, damage accumulates across a career and even the 0.15–0.55 band
  eventually destroys.
- **A hit still forces time compression to 1×** (`0x6351E7`). Unchanged, and
  desirable: it is the only reliable way the player notices.
- **Cosmetic debt:** string 4893, *"If we don't cut power now, we're risking a
  breakdown!"*, fires on partial damage. Under a speed-independent model its advice
  is misleading. Re-pointing it to another `en_menu.txt` id is optional polish.
- **Unverified:** that `hsie.act` parses an exponent as small as `e-8`; that crew
  repair clears the diesel damage field.

### Does this still deserve `.scratch/diesel-damage-patch/`?

**No — it is a feature of the GUI patcher, not an effort of its own.** The
unsolved persistence problem is gone and there is no runtime subsystem left to
build. Folded into ticket 07's v1 feature list.

But it did **not** stay the cheap candidate it briefly looked like. Final surface:

- 8 `hsie.ini` keys (needs a comment-preserving, key-surgical INI writer)
- 4 `.pcode` immediates in `sh3.exe`
- 1 `.pcode` **function rewrite** (`0x635818`, 21 bytes into 38)
- 2 GUI input fields with validation and live derived stats
- **two write targets in one feature panel**, with read-back from both

So it is now the candidate that **exercises the most of the patcher's
architecture** — which arguably makes it a better v1 proof than hull repair, not
a worse one. Sites and numbers: `docs/re-notes/04-diesel-damage-signature.md`.

---

## Answer (1st pass — SUPERSEDED 2026-08-15)

> Kept for the record. The eight-hour overload gate below was resolved against the
> ticket's original premise and is **no longer the decision**. Its RE findings
> remain valid; only the design conclusions are withdrawn.

Resolved 2026-08-15. Full RE write-up:
`docs/re-notes/04-diesel-damage-signature.md`.

### What h.sie's mod actually is

An **overload** mechanic, not a wear mechanic. Per h.sie's V16 manual §21 and
confirmed instruction-by-instruction from the cave at `0x635000`/`0x635049`:

```
p_ds = p_dso x Factor x MTS^Speed-Exponent      (MTS = machine telegraph, 0..1)
```

A per-game-second Bernoulli trial, rolled once per real second with time
compression charged as `1-(1-p)^TC` trials. `p_dso` = 2e-4; Factor/Exponent
switch on how many diesels are already damaged (`1.0/200` → `4.0/20` → `8.0/10`).
Damage lands on one of two floats (`0.0` = fine, `1.0` = destroyed),
`+= rand(0.01…0.59)` for the first hit, `rand(0.30…1.00)` after. It also forces
time compression back to 1x on every hit.

### The ticket's premise — "eight hours" — was half right, and the half matters

The user was right that h.sie's documentation has a timing component; the
originally-recorded reading that there was none was wrong. But it is **T50** —
`ln(0.5)/ln(1-p_ds)`, a formula h.sie states in `hsie.ini` — which is a *derived
output* of the per-second rate, not an input timer. Verified: **every documented
T50 row reproduces from the shipped constants.**

So there is genuinely **no elapsed-running-time term** anywhere: the model is
memoryless. Seven hours at flank with no damage leaves the eighth hour exactly as
risky as the first. `0x63CF13` is referenced **exactly once** in the whole image,
and `hsie.act` contains **zero** references to any diesel address — so the cave
at `0x635049` is the entire mod, and there is nowhere else for a timer to hide.

### Decisions

- **Model shape: a gate, not a retune.** APE accumulates **game-seconds spent at
  overload**, and h.sie's damage model is **not called at all** until that
  accumulator passes **eight hours** — zero chance of damage before then. After
  the gate opens, h.sie's model applies as-is, with the option to expose edited
  constants later.
- **Overload = `MTS == 1.0` exactly.** Matches h.sie's framing and OAE's stock
  data, where nothing else reaches 0.97.
- **The accumulator resets each patrol.**
- **It must survive save/reload.** Session-only was rejected as an obvious
  save-scum exploit that would make "eight hours" meaningless. This implies APE
  needs its own persisted state keyed to career/patrol — **the first genuinely
  hard problem in this feature, and it is not yet solved.**
- **Delivery: a pure `ape.act` runtime feature.** Not the GUI patcher. APE holds
  `0x63CF13` at `0x00` and writes `0xFF` when the accumulator opens the gate; the
  cave re-reads the flag every second, so the gate is immediate and the disabled
  path is side-effect-free (one wasted tick on flip — see RE note §5a).
- **The user intends to ship with the h.sie patch ON.** It is off in every
  surveyed install because both OAE and GWX-KC consider it ahistorical as
  written — which is precisely what this work is meant to fix, not evidence
  against doing it.

### Findings that reach beyond this ticket

- **⚠ `hsie.ini` is live and clobbers `.pconst` at load.** `hsie.act` reads
  `./hsie.ini`, `VirtualProtect`s the section, and writes 24 floats over
  addresses `0x63C000`–`0x63C0E8`. Proven: the exe holds 0.15/0.10/0.03 where the
  ini holds 0.35/0.20/0.05, and the ini wins. **Any static byte APE writes into
  that list is overwritten at runtime**, and verify-before-write cannot catch it
  because the on-disk bytes are correct until `hsie.act` loads. All eight diesel
  tunables are in the list; **the hull-repair site is not.** Raised as ticket 09.
  This also *answers* the map's open "does `hsie.ini` still do anything?" — yes,
  decisively.
- **⚠ Tension with ticket 05.** That ticket ruled APE "reads h.sie's flag table
  but never writes it". This feature's central mechanism is APE writing
  `0x63CF13` — in live memory, not on disk. Arguably a different act, but a
  boundary to reopen deliberately rather than cross quietly. Raised as ticket 10.
- **Free anchor:** `[0x0063E1D4]` holds the machine-telegraph value, refreshed
  once per second at a fixed address, **whether or not Diesel Damage is enabled**
  (the read precedes the flag test, and every h.sie variant carries the same
  cave). APE can read it instead of walking the pointer chain.
- **`Val13` drift:** h.sie's documented two-damaged Factor is 16.0; this install
  ships 8.0, running that case at half the documented risk. Cases a) and b) match
  exactly.
- **Data prerequisite satisfied:** every `HumanPlayable=YES` boat ships
  `AheadFlank=1.00` and `AheadFull=0.85`. No `.cfg` edit needed.

### Ready to spin up `.scratch/diesel-damage-patch/`?

**Yes** — unlike hull repair (ticket 03), this deliverable does not wait on the
patcher: it is `ape.act` runtime logic, and `ape.act` exists. Run `/to-tickets`
against this answer to create the effort. Two things should land as its first
issues:

1. **Persisting the accumulator across save/reload** — the unsolved problem
   above, and the one most likely to reshape the design.
2. **Signature + uniqueness proof for `0x63CF13` and `0x63E1D4`** per ADR-0003,
   since both are absolute VAs from one variant set.

Ticket 10 (APE writing h.sie flags) should settle before that effort ships,
though not before it starts.

## Caveats inherited

- Verified against the sibling-build variant set in `data/exe_with_hsie_patches/`,
  not registered build `4B6BEE20`.
- VAs are absolute; ADR-0003 wants wildcarded AOB signatures with uniqueness
  proofs. Not yet done for this cave.

## Amendment 2026-08-16 — the flat exponent needs a companion idle cutoff

Found by black-box play in
[ticket 12](12-diesel-levers-blackbox-test.md), not by RE.

This ticket's resolution sets **Speed-Exponent = 0.0** so that wear is
independent of speed ("any time the diesels run"). In play that also fires at
**all stop, at the full flank rate**: the formula evaluates `pow(0.0, 0.0)`,
which CRT `pow` returns as **`1.0`** per IEEE-754/C99. There is no "engines
idle" case in the model at all.

**Amended model — exponent `0.01`, threshold derived:**

| Key | Was (this ticket) | Now |
|---|---|---|
| `Val14/15/16` Speed-Exponent | `0.0` | **`0.01`** |
| `Val10` threshold | `1e-6` (h.sie's) | **`p_dso × 0.977237`** (derived) |

`Speed^0.01` is monotonic, so `p_ds > Val10` ⟺ `telegraph > 0.1` exactly —
reusing h.sie's existing threshold test at `0x635129` with **no code patch**.
The cutoff sits below OAE's slowest telegraph (`AheadSlow = 0.20`) and KC's
(`0.40`), and above any not-quite-zero idle value. Flatness is preserved: the
rate varies **1.6% across telegraph 0.20 → 1.00**, against this ticket's intent
of a speed-independent rate. Full table and the precision argument are in
ticket 12.

Everything else in this ticket's resolution stands: Factors still `1.0`, the
`0x635818` rewrite still required, the `rand(0.15…0.55)` band unchanged.

⚠ Two knock-ons:

- **`Val10` becomes a derived site, not a constant** — the patcher must
  recompute it from the rate field on every write (tickets 07/08).
- **Both defects found in this ticket's retuning were endpoint defects** — the
  `2.98e-8` float32 lattice floor and now `pow(0, 0)`. Both were invisible at
  h.sie's tuning. The map's standing note about checking a retuned parameter's
  *code path* should be read as also covering its **range endpoints**.
