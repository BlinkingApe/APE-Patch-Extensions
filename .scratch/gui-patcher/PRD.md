# APE Patch Extensions — GUI patcher, beta

Status: ready-for-agent

Supersedes nothing. Discharges the effort shape called for in
[umbrella ticket 07](../umbrella-roadmap/issues/07-patcher-v1-scope.md) ("one new
`.scratch/gui-patcher/`, built prototype-first"). The prototype round is done and
recorded in [`prototype/README.md`](prototype/README.md); this is the spec for the
first real, usable build.

## Problem Statement

APE's two v1 features — hull repair at sea and diesel damage — currently exist only
as proven byte offsets in RE notes and as hand-edits made during ticket 12's
black-box test. There is no way for a player to turn them on.

The prototype answered *what the tool should look like* (variant C, a
review-and-commit dashboard) but it is deliberately a shell: it reads nothing from
a real install, writes nothing to one, fakes the two most important environment
states behind visible "sim" checkboxes, and carries two rejected layout variants
plus a black variant-switcher bar. It also cannot be handed to a player, because it
never opens a file.

Three further problems block a beta:

- **It is not culture-safe.** Some formatting is invariant and some is not. This is
  not cosmetic: `hsie.ini` is parsed by h.sie's own code, and a machine running a
  comma-decimal locale would have APE write `Val9 = 4,1667e-8`, which h.sie cannot
  read. The failure is silent — the mod goes inert with no error.
- **It has no identity.** The Blinking Ape portrait is embedded and drawn in the
  header, but the window's title-bar icon, taskbar button, alt-tab entry, and the
  exe's own file thumbnail in Explorer all show the default .NET application icon.
- **The diesel destroy statistic misleads.** "Two unrepaired hits — 3.1% destroy it
  outright" reads as a risk figure. It is a conditional probability resting on a
  chain of prerequisites the line does not state.

## Solution

Fold prototype variant C into a real, single-window tool that reads an actual OAE
1.5.2 install, shows the player what is currently applied, lets them edit the two
features, and commits every change in one verified write — with a genuine revert
path when a feature is unchecked.

Everything that existed only to support the prototype round goes: variants A and B,
the variant-switcher bar, the arrow-key cycling, the command-line variant/sim
arguments, and both simulation checkboxes. The states those checkboxes faked —
unsupported build, h.sie's Diesel Damage flag off — become real reads, which is the
whole point: h.sie's flag is `0x00` in every surveyed install, so the
disabled-with-explanation path is what a real user sees first.

The tool becomes culture-invariant end to end, ships the Blinking Ape as its
application icon everywhere Windows shows one, drops backup handling entirely, and
replaces the destroy percentage with the threshold it actually encodes.

## User Stories

### Opening the tool

1. As a player, I want APE to find my game install by looking at its own location
   in the game root, so that I do not have to tell it where SH3 is.
2. As a player, I want to see the install path, build name, h.sie version, and file
   hash in the header, so that I can confirm APE is looking at the install I think
   it is.
3. As a player, I want an unmistakable "Install verified" state, so that I know APE
   recognises my build before I change anything.
4. As a player running an unsupported build, I want APE to refuse in the header and
   disable every control, so that I cannot accidentally write to a binary APE was
   never proven against.
5. As a player running an unsupported build, I want to be told which build APE does
   support, so that I understand why it refused rather than just that it did.
6. As a player, I want APE to open showing the values my install currently holds,
   not defaults, so that the window always describes reality.
7. As a player who has already applied APE, I want the feature checkboxes to open
   ticked with my previously chosen values, so that I can adjust rather than
   re-enter them.
8. As a player, I want the window's title-bar icon, taskbar button, and alt-tab
   entry to show the Blinking Ape, so that the tool is identifiable while it is
   running.
9. As a player, I want the exe's file icon in Explorer to show the Blinking Ape, so
   that I can find it in the game folder among SH3's own executables.
10. As a player on a high-DPI display, I want the window to render at the right
    size with crisp text, so that it is legible without squinting.

### Hull repair at sea

11. As a player, I want a single checkbox that turns APE's hull-repair change on
    and off, so that the decision is one act.
12. As a player, I want the minimum and maximum repair percentages as editable
    fields, so that I can tune how much hull the crew restores per event.
13. As a player, I want to be told in plain words that unchecking the feature puts
    h.sie's own 5–20% roll back, so that I know what "off" means.
14. As a player, I want to be warned when I set both bounds to zero, so that I
    understand APE stays applied while the crew repairs nothing — which is not the
    same as unchecking.
15. As a player, I want to be stopped from setting a minimum above the maximum, so
    that I cannot commit an incoherent range.
16. As a player, I want the fields disabled while the feature is unchecked, so that
    it is obvious they only matter when the feature is on.

### Diesel damage

17. As a player whose h.sie Diesel Damage switch is off, I want the feature shown
    disabled with an explanation, so that I know why I cannot use it.
18. As a player in that state, I want to be told to enable it in HsieOptionsSelector
    and reopen APE, so that I have a concrete next step.
19. As a player in that state, I want to be told APE never writes h.sie's own
    switches, so that I understand the boundary between the two tools.
20. As a player, I want to set the damage rate as events per 10,000 hours of diesel
    running, so that I am tuning a figure I can reason about rather than a raw
    per-second probability.
21. As a player, I want to set the damage band as a low/high pair, so that I control
    how severe each incident is.
22. As a player, I want to be stopped from committing a band where the low is not
    strictly below the high, so that the range is always coherent.
23. As a player, I want to see the mean damage per event derived live from my band,
    so that I can judge severity without doing the arithmetic.
24. As a player, I want to be told plainly that no single hit can ever destroy an
    engine while the maximum is below 1.00, so that I know the crew-repairable
    guarantee holds.
25. As a player, I want to be told that two hits cannot combine to destroy an engine
    while the maximum is 0.50 or below, so that I can choose a band that makes
    destruction impossible rather than merely unlikely.
26. As a player who raises the maximum above 0.50, I want the tool to say so and
    state the conditions under which destruction becomes possible, so that I am
    making an informed choice rather than tripping over a hidden threshold.
27. As a player, I want that warning to name its conditions — two hits on the same
    engine, the first still unrepaired — so that I do not read it as a
    per-patrol risk figure.
28. As a player, I want to see the resulting per-game-second wear rate, so that I
    can sanity-check what my events-per-10,000-hours figure became.
29. As a player who sets a very low rate, I want to be told when it falls below
    float32 epsilon and why it still works, so that I trust a number that would
    silently do nothing on an unpatched install.
30. As a player, I want to be told that diesels below one tenth telegraph count as
    idle and take no wear, so that I know the feature does not fire at all stop.
31. As a player, I want to be told in plain words that unchecking the feature puts
    h.sie's own damage model back, so that "off" means the same thing here as it
    does for hull repair.

### Reviewing and committing

32. As a player, I want a running plain-English list of everything my current
    settings will change, so that I can read the consequences rather than the
    edits.
33. As a player, I want that list to speak only in game terms — never file names,
    never addresses — so that it is meaningful to someone who has never opened a
    hex editor.
34. As a player, I want the list grouped by feature, so that I can see which change
    belongs to which checkbox.
35. As a player, I want a count of how many features will change, so that I can see
    the size of the commit at a glance.
36. As a player who has changed nothing, I want the list to say so explicitly, so
    that an empty panel is never ambiguous.
37. As a player, I want a single Apply button that commits everything at once, so
    that I am not applying features one at a time in an order I have to think
    about.
38. As a player, I want the Apply button to read "Nothing to apply" and be disabled
    when there is nothing to do, so that the button always describes its own effect.
39. As a player, I want APE to verify the original bytes at every site before
    writing anything, so that a mismatched install is refused rather than corrupted.
40. As a player, I want the whole commit to abort if any single site fails
    verification, so that I never end up half-patched.
41. As a player, I want to be told which site failed and what was found there, so
    that a refusal is diagnosable rather than opaque.
42. As a player, I want to be told plainly that unchecking a feature restores the
    original bytes and that this is the uninstaller, so that I do not go looking
    for one.
43. As a player, I want unchecking a feature and applying to restore the exact
    original bytes and the exact original `hsie.ini` lines, so that reverting is
    byte-identical and not merely approximate.
44. As a player, I want to see confirmation of what was written after a successful
    apply, so that I know the commit landed.
45. As a player, I want the window to reflect the newly written state after an
    apply, so that what I see continues to describe the install.
46. As a player, I want APE to refuse to write while SH3 is running, so that I do
    not patch a binary that is currently loaded.
47. As a player, I want a clear error if APE cannot write to the game folder, so
    that a permissions problem is legible rather than a silent no-op.

### Correctness under any locale

48. As a player on a comma-decimal locale, I want APE to write `hsie.ini` values in
    the form h.sie's own parser expects, so that the mod does not silently go inert.
49. As a player on a comma-decimal locale, I want APE to read back the values my
    install holds correctly, so that the window does not show me wrong numbers.
50. As a player on any locale, I want the number fields to accept and display
    decimals consistently, so that typing a value does what I expect.
51. As a player on any locale, I want APE's derived statistics to read the same way
    they would on any other machine, so that a screenshot means the same thing to
    everyone.

### Identity

52. As a perceptive player, I want the product name to quietly reward a second look
    once I notice what the acronym expands into, so that the tool has a little
    character without ever explaining itself to me.

## Implementation Decisions

### The shell collapses to one window

Variants A and B, the `Shell` host with its floating black bar, the arrow-key
cycling, `VariantName`, and the `--variant=` / bare-letter argument parsing are all
deleted. Variant C becomes the form itself. The whole `prototype/` directory retires
to a throwaway branch once this lands, per its own README.

The `--diesel` and `--unsupported` arguments and the two `SimBox` checkboxes go with
it. `Model.SimUnsupported` and `Model.SimDieselFlagOn` are replaced by properties
derived from a real install read. `Model.BuildName`, `HsieVersion`, `Sha`,
`InstallPath`, `Supported`, and `DieselAvailable` stop returning literals and become
projections of that read.

### One seam: the patch session

The tool is split so that everything worth testing sits behind a single seam, and
the WinForms layer holds no logic of its own.

**`PatchSession`** — opened against a game directory, it is the only thing in the
tool that touches a file. It exposes:

- **Open** — locate and read `sh3.exe` and `hsie.ini`, identify the build, read
  h.sie's flag table, verify context bytes at every site, and produce an
  **install report** (path, build, h.sie version, hash, supported yes/no, per-patch
  availability) plus a **current-state model** populated from the install's actual
  bytes and ini lines.
- **Apply** — take a desired-state model, verify every site again, and either write
  all of it or none of it, returning a **write report** naming each site touched.

The desired-state model is a plain object with no I/O and no WinForms dependency,
carrying the two features' fields and every derived statistic as computed
properties. Testing the session therefore also tests the arithmetic, without adding
a second seam.

The form's only job is to bind controls to that model, call `Apply`, and render the
reports. No probability, no offset, and no encoding rule lives in the UI layer.

### Real install detection replaces the sims

The header is populated from the install report:

- **Build identity** — SHA-256 of `sh3.exe` against the version allowlist. v1 admits
  **OAE 1.5.2 with h.sie `v 1.6E1` only**; anything else is refused with no writes
  (umbrella ticket 07).
- **h.sie Diesel Damage flag** — read from the flag table at `0x63CF13`, entry #20.
  `0xFF` enables the feature, `0x00` shows it disabled-with-explanation. **APE reads
  the flag table and never writes it** (umbrella ticket 05, decision A-2). Since the
  flag is `0x00` in every surveyed install, the disabled path is the default
  experience and must be treated as the primary state, not an edge case.
- **Read-back is dual-store.** The diesel panel's rate comes from `hsie.ini`'s
  `Val9`; its damage band comes from `sh3.exe`'s four `.pcode` immediates. Hull
  repair's range comes from `sh3.exe` alone. A feature is shown as applied when its
  sites hold APE values rather than h.sie's originals.

### The write path

Driven by the manifest schema settled in
[umbrella ticket 08](../umbrella-roadmap/issues/08-signature-manifest-format.md):
patches contain sites, each site declares a store, a field encoding
(`byte` / `float32` / `operand-immediate` / `fixed-rewrite`), a per-store locator,
invariant context bytes, and its complete original byte run or ini line.

- **Verify before write, all-or-nothing.** Every site's context bytes are checked
  before any byte is written. One mismatch aborts the entire commit and names the
  offending site.
- **`hsie.ini` is edited in place, surgically.** Named keys only; comments,
  ordering, whitespace, and every key APE does not own are preserved. A replacement
  file is never shipped — OAE has heavily customised the surrounding `Val`s
  (umbrella ticket 04).
- **`Val10` stays derived and hidden.** Recomputed as `p_dso × 0.977237` on every
  write, never shown in the UI, never editable. This is the idle-cutoff amendment
  from umbrella ticket 12.
- **`Val14/15/16` are written as `0.01`, not `0.0`** — the amended exponent that
  makes `p_ds > Val10` equivalent to `telegraph > 0.1`, giving the idle cutoff with
  no code patch.
- **The `0x635818` rewrite is a `fixed-rewrite` site** — 21 bytes into a 38-byte
  body, `int3`-padded, no user-facing field. Written when diesel damage is enabled,
  restored from its stored original run when unchecked.
- **Revert is unchecking.** Applying with a feature unchecked restores that
  feature's stored original bytes and ini lines exactly. There is no separate
  uninstall command.
- **The manifest is embedded as a resource** and guarded by the build-time
  regenerate-and-diff step from ticket 08 — the C# build re-runs
  `tools/re-static/`'s generator and fails on drift.

### Backups: removed entirely

APE neither checks for nor creates `sh3_BACKUP.exe` / `hsie_BACKUP.ini`. The
"backups present" row leaves the verification header, and no backup logic is
written. Safety rests on two mechanisms already in the design: **verify-before-write**
refuses a commit against unexpected bytes, and every site carries its **complete
original run**, making unchecking a byte-exact revert. The footer keeps its
verify-before-write note and its "that is the uninstaller" line, which are now the
only safety story the tool tells — and they should be told accurately.

Consequence worth stating: a player who patches with a third-party tool between two
APE sessions will be refused rather than silently overwritten, which is the intended
behaviour.

### Culture invariance

Two distinct problems, both fixed.

- **Every format and parse is explicitly `CultureInfo.InvariantCulture`.** This
  covers `hsie.ini` writes and reads above all — h.sie's parser expects
  `4.1667e-8` and a comma-decimal machine would otherwise write `4,1667e-8` and
  render the mod inert with no error and no in-game symptom. It also covers hashes,
  hex, and every derived statistic. The existing `UI.Sci` / `UI.F1` / `UI.F2`
  helpers already do this; the requirement is that nothing bypasses them.
- **The UI thread's culture is pinned to the invariant culture at startup.**
  `NumericUpDown` formats and parses its text through the *current* culture, so
  without this a German user sees and types `0,55` while the model reasons in
  `0.55`. Pinning the thread culture makes the control agree with the model
  everywhere.

The tool ships in English only; there is no localisation to preserve.

### The destroy statistic becomes a threshold

The existing `PTwoHitsDestroy` arithmetic is **correct** — it computes
P(X + Y ≥ 1.0) for X, Y ~ U(lo, hi), which at the `0.15…0.55` default is
**3.125%**, matching umbrella ticket 04. The magnitude constraint is already inside
that integral: with a maximum of `0.55`, reaching 1.0 requires **both** hits to land
at or above `1 − hi = 0.45`.

What is wrong is the presentation. `"Two unrepaired hits  3.1% destroy it outright"`
reads as a risk figure, but 3.1% is *conditional* on two hits landing on the same
engine with the first still unrepaired — and at 1.5 events per 10,000 hours a career
sees two hits at all only about 44% of the time, before any repair is modelled. It
also states a probability whose most important property is not probabilistic at all:
below a maximum of `0.50` it is exactly zero.

**Decision: lead with the threshold, and show the percentage only when it is
non-zero and explicitly conditional.** The derived-stats block becomes:

```
Mean damage per event   0.35 of the engine
One hit never destroys an engine.
Two hits cannot destroy one either.       <- when hi <= 0.50
Wear rate               4.1667e-08 per game-second
```

```
Mean damage per event   0.40 of the engine
One hit never destroys an engine.
Two hits on the same engine, the first
still unrepaired, destroy it 3.1% of the time.   <- when hi > 0.50, in warn colour
Wear rate               4.4444e-08 per game-second
```

Rules:

- Destruction is **possible iff `hi > 0.50`**. The exactly-`hi == 0.50` case can only
  reach 1.0 if two draws both land exactly on the maximum — a measure-zero event —
  and is deliberately rounded to "cannot destroy" rather than shown as `0.0%`,
  which would contradict its own heading.
- The "one hit never destroys" line holds for any `hi < 1.00` and is stated
  unconditionally, because it is the crew-repairable guarantee the whole band
  decision rests on (umbrella ticket 04).
- The percentage line is the only one that turns warn-coloured, and it turns at the
  `hi > 0.50` threshold, replacing the prototype's arbitrary `> 5.0` percentage
  trigger.
- The same wording — threshold first, conditions named — is used in the
  right-hand review list, replacing "Two hits left unrepaired destroy it 3.1% of the
  time".

This also settles, in the UI rather than by decree, the item umbrella ticket 07
carried forward as *"whether `hi` is capped below 0.5 so two unrepaired hits cannot
sum to 1.0"*: **`hi` is not capped.** The full `0.01…0.99` range stays available for
tuning sweeps, and the threshold line tells the player exactly what crossing `0.50`
costs them.

### Application icon

`data/art/blinkingape.png` (187×251) is already embedded and drawn in the header;
that part changes in no way. What is missing is a `.ico`.

- A multi-resolution icon (16, 32, 48, 256 px) is generated from the same source art
  and checked in beside it. The portrait art is padded to square rather than
  stretched, so the ape is not distorted at small sizes.
- The icon is bound to the **exe** at build time (the compiler's win32 icon input),
  which drives the Explorer thumbnail and the taskbar.
- It is also assigned to the **form** so the title bar and alt-tab entry match.
- Both must be set. Setting only the exe icon leaves the title bar on the default;
  setting only the form icon leaves Explorer on the default. The prototype set
  neither, which is why both still show the .NET default.

### The recursive name, played straight

**APE Patch Extensions** expands into itself — `A` is `APE`, `P` is `Patch`, `E` is
`Extensions` — and nothing in the tool currently notices. The header brand label
picks the mapping out in type, so it is *available* to a reader who looks twice.

**Three runs, two tiers of emphasis:**

| Run | Weight | Role |
|---|---|---|
| `APE` | **bold** | the product name, read as branding |
| `P`, `E` of `Patch Extensions` | **semibold** | the quiet pointer back to it |
| everything else | regular | |

Note the prototype does **not** currently bold `APE` — the brand is one
regular-weight label, and the bold line in that header is *"Install verified"*
below it. So this is new emphasis on both tiers, not a carry-forward.

The two tiers are load-bearing, not decorative. Setting `APE` and the `P`/`E` at the
same weight puts five of the twenty letters in bold and the joke stops being a joke;
a bold brand with a lighter second tier keeps the name reading as a name at a glance,
and lets the pointer resolve only on a second look. If only one weight above regular
is workable, **drop the `P`/`E` tier and keep `APE` bold** — a plain brand is better
than a loud gag.

`A` gets no second-tier emphasis, because it is already carrying the bold `APE` and
because a reader who spots `P` and `E` supplies the third mapping themselves. That
is the whole effect.

Constraints, because this is exactly the kind of touch that goes wrong by being
tried too hard:

- **The header brand label only.** Not the window title, not the taskbar, not the
  review list, not the write confirmation. Those all render the name plainly.
- **Weight alone.** No colour shift, no size change, no letter-spacing, no capital
  variation.
- **Never explained.** No tooltip, no About box gloss, no footnote. A user who does
  not notice must not be able to tell there was anything to notice.
- **If it reads as a typo, it is too strong.** The bar is that at a glance the name
  looks normally set apart from a bold product name, which is unremarkable.

Implementation notes:

- A single WinForms `Label` cannot mix weights, so the brand becomes a small
  custom-drawn label that measures and draws its runs in sequence. That measurement
  must go through the same real text-measurement path as the review list below, so
  the letters stay correctly kerned and spaced at every DPI scale — a hand-summed
  x-offset will drift on a scaled display and turn a subtle touch into a visible
  defect. With five runs rather than three (`APE`, ` `, `P`, `atch `, `E`,
  `xtensions`) the drift risk is higher, not lower.
- Semibold is a **separate font family**, not a `FontStyle` — the header font
  derives from `SystemFonts.MessageBoxFont`, which is Segoe UI on the supported
  platform, and its semibold face is requested by family name. Resolve it once at
  startup and fall back to regular (not bold) if it is unavailable, per the
  one-tier rule above.

### Layout at beta quality

The prototype positions controls absolutely and hard-codes a 520×420 review list
that neither reflows nor scrolls with the window. For a tool a player resizes:

- The review list fills its column and scrolls when its content overflows.
- `Line`'s wrap estimate — `18 * (1 + text.Length / 78)` — is replaced by real text
  measurement, so no line is ever clipped by a long install path or a long warning.
- The window declares per-monitor DPI awareness so it renders correctly on scaled
  displays.
- Tab order runs top to bottom through the left column and ends on Apply, so the
  window is keyboard-operable.

## Testing Decisions

A good test here exercises **observable behaviour through `PatchSession`** — open a
prepared game directory, assert what the tool reports; apply a desired state, assert
what the bytes and the ini text became. It never reaches for a private method, never
asserts on control positions, and never asserts that a particular write helper was
called.

Prior art in this repo is `tests/damage_band_test.cpp` (a pure numeric model tested
directly) and the in-play rig from umbrella ticket 12. The C# tool needs its own
harness; there is none yet.

**What is tested, all through the one seam:**

1. **Detection** — a supported install is reported supported; a mutated `sh3.exe`
   hash is reported unsupported; a `0x00` diesel flag reports the feature
   unavailable while a `0xFF` flag reports it available. These are the three states
   the deleted sim checkboxes used to fake, and they become the first tests written.
2. **Read-back** — a directory holding h.sie's original bytes reports both features
   unapplied with h.sie's values; a directory holding APE's bytes reports them
   applied with APE's values; the diesel panel's two fields are shown to come from
   two different stores.
3. **Round-trip revert** — apply both features, then apply with both unchecked, and
   assert `sh3.exe` and `hsie.ini` are **byte-identical** to the starting fixtures.
   This is the test that stands in for the deleted backup logic and it is the most
   important one in the suite.
4. **Ini surgery** — after a write, every `Val` APE does not own is unchanged, and
   all comments, ordering, and blank lines are preserved. Asserted against a real
   OAE `hsie.ini` fixture, not a minimal one.
5. **Verify-before-write** — corrupt one site's context bytes and assert the commit
   is refused, names that site, and leaves **both** files untouched. Run once per
   store so the all-or-nothing guarantee is proven across the `sh3.exe`/`hsie.ini`
   boundary.
6. **Derived arithmetic**, asserted on the model the session hands out: `p_dso` from
   the rate; `Val10 = p_dso × 0.977237`; mean damage; the float32-epsilon flag at
   the `2.98e-8` boundary; and the destroy threshold at `hi` of `0.49`, `0.50`,
   `0.51`, and `0.55` — the last of which must give **3.125%**, matching umbrella
   ticket 04.
7. **Culture** — the entire suite runs a second time under a comma-decimal culture
   (e.g. `de-DE`) and must produce byte-identical output. This is the test that
   would have caught the `hsie.ini` failure, and it is cheap: one parameterised
   fixture over the same cases.
8. **Manifest freshness** — the build-time regenerate-and-diff step from ticket 08
   fails the build when the checked-in manifest drifts from what
   `tools/re-static/` produces.

**Fixtures** come from `data/exe_with_hsie_patches/` and a copy of the real OAE
1.5.2 `hsie.ini`, copied into a temp directory per test so no test mutates a
checked-in file. Note that the live OAE 1.5.2 install is currently **left in ticket
12's test state** and must be reverted before it can serve as a clean baseline.

**Not covered by automated tests, and deliberately so:** whether the written values
produce the intended behaviour in play. That is umbrella ticket 07's done bar — the
shipped patcher writes both features on the real install, the effects are observed
in play, and unchecking restores the original bytes — and it stays a manual gate.

## Out of Scope

- **`ape.act` as a write target**, its deploy/patch/delete lifecycle, and the
  crush-depth constants — deferred whole out of v1 by umbrella ticket 07, blocked on
  crush-depth issue 01's four dynamic hunts.
- **The `LoadLibrary` call-site patch** — mandatory only if `ape.act` must load, and
  nothing loads it in v1.
- **Any build other than OAE 1.5.2 / h.sie `v 1.6E1`.** h.sie `v 1.6B1` and OAE
  1.5.3.7 require every site independently re-proven against a real binary of that
  version (ticket 08's admission bar) before their entries join the version map.
- **The docking mod** — still fog.
- **Writing h.sie's own flag table.** APE reads `0x63CF13` and never writes it.
  Umbrella ticket 10 is closed as moot.
- **Localisation.** English only; culture invariance is a correctness requirement,
  not a step toward translation.
- **Re-splitting the diesel damage bands.** The escalated pair stays addressable and
  is written with the same values as the first-hit pair, leaving the door open, but
  no UI exposes it.
- **Exposing `Val10`, the Factors, or the Speed-Exponents as fields.** They are
  derived or pinned.
- **Re-pointing h.sie's string 4893** ("If we don't cut power now…"), whose advice is
  misleading under a speed-independent model. Cosmetic debt, recorded in umbrella
  ticket 04.
- **An installer, an updater, or any network access.** One exe in the game root.

## Further Notes

- **Two assumptions this design rests on remain unproven and should be stated where
  they matter.** First, that crew repair clears the diesel damage field — the entire
  "one hit never destroys" guarantee is worthless if damage accumulates across a
  career. Second, the surfaced gate at `0x6350CA`, which umbrella ticket 07 resolved
  by scope pivot (trust h.sie's mechanics) rather than by proof. Neither blocks the
  beta; both are worth re-reading before the done-bar play session.
- **Both defects found while retuning diesel damage were endpoint defects** — the
  `2.98e-8` float32 lattice floor and `pow(0, 0) == 1.0`. Both were invisible at
  h.sie's own tuning. Any new field this tool exposes deserves its range endpoints
  exercised, not just its default.
- **The `hsie.ini` runtime clobber inverts here.** Umbrella ticket 09 established
  that `hsie.act` overwrites `0x63C000`–`0x63C0E8` at load, which makes
  verify-before-write insufficient on its own for `.pconst` sites. No v1 site lands
  in that range: the diesel rate is routed to `hsie.ini` *because* of the clobber,
  and hull repair's site is outside it. This is why per-site store routing exists and
  it must not be quietly collapsed.
- **The disabled-with-explanation path is the default experience**, not an edge case.
  It deserves the same layout care as the enabled path, and it should be the first
  screen anyone reviews.
- **Two decisions taken during the build depart from this PRD's letter**, both
  because following it literally would have broken a user story it also states.

  **1 — Build identity is a *normalised* hash, not `SHA-256` of `sh3.exe`.**
  "Build identity — SHA-256 of `sh3.exe` against the version allowlist" cannot
  hold, because APE's own writes change that hash: the first successful apply
  would make the install unrecognisable, and stories 7 and 45 (open ticked with
  my previously chosen values; reflect the newly written state) become
  impossible. The identity is therefore taken over a copy with **every byte run
  APE owns reset to its manifest original and h.sie's 22-byte flag table
  zeroed**. Two consequences, both wanted: an install APE has patched still
  identifies as the same build, and a `HsieOptionsSelector` run — which rewrites
  only flag bytes — does not change a build's identity either. The plain file
  hash is still shown in the header (story 2); it just decides nothing. The
  allowlist is unchanged in substance: one build, OAE 1.5.2 / h.sie `v 1.6E1`,
  recorded as `36d66176…` normalised alongside the pristine `51d14ca3…`.

  **2 — The diesel panel opens on APE's defaults when the feature is not
  applied.** Story 6 asks the window to describe reality rather than defaults.
  It does, for hull repair, whose roll operands read back in every state — and
  h.sie's own `5–20` happens to be APE's default anyway. It cannot for diesel
  damage: h.sie ships **two** bands selected on damage state, and APE's model has
  one, so an unapplied install has no single band to show. The band and rate
  fields therefore open on APE's defaults, the state line says *"Not applied —
  h.sie's own damage model is untouched"*, and the review list states what would
  change. Once applied, both fields read back from the install, from their two
  different stores.

- **A third clarification, discovered by test rather than decided.** Because APE
  holds no state outside the bytes, a feature set to exactly what h.sie already
  ships is not a change and is not written — ticking hull repair and leaving it
  at `5–20` writes nothing, and the button correctly reads *"Nothing to apply"*.
  `Val11` behaves the same way inside diesel damage: h.sie already ships
  `Factor = 1.0` for zero damaged diesels, so applying the feature writes 12
  `hsie.ini`/`sh3.exe` sites, not 13. This is the honest reading of "a feature is
  shown as applied when its sites hold APE values rather than h.sie's
  originals", and it is what makes the change count and the write report the
  same claim.

- **An ADR is still owed** reconciling this tool's fixed-offset, authoring-time
  proof model with ADR-0003's runtime signature scanning — which remains correct for
  `ape.act`'s live anchors. Tracked in the umbrella map's documentation-debt entry.
