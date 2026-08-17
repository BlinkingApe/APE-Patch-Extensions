# PROTOTYPE — APE patcher main window

**Throwaway.** Variants of the APE patcher's main window, switchable from a floating
bottom bar, in C# WinForms (.NET Framework 4.x, `csc.exe` — the toolchain ticket 05
picked for the real tool). No write path, no tests, no persistence. Apply/Revert pop a
MessageBox describing the write that *would* happen.

## Run

```
.scratch\gui-patcher\prototype\run.cmd                  # C, the chosen variant
.scratch\gui-patcher\prototype\run.cmd A                # A or B for comparison
.scratch\gui-patcher\prototype\run.cmd C --diesel       # h.sie's diesel flag simulated ON
.scratch\gui-patcher\prototype\run.cmd C --unsupported  # straight to the allowlist refusal
```

`←` / `→` or the arrows in the black bar cycle variants. Close any running instance
before re-running — it holds a lock on the exe.

## Decision — 2026-08-17

**Variant C wins: a review-and-commit dashboard.** Install-verification header,
feature toggles and fields down the left, a plain-English "what will change" list on
the right, and **one commit** for everything — not per-feature apply (B) and not a
master/detail pane that hides all but one feature (A).

Four changes taken on C at the same time, all applied:

1. **The damage band steps by `0.01` per click**, not `0.05`. (Rate stays at `0.1`,
   since it is an events-per-10,000-hours figure.)
2. **The derived `Val10` is noise — removed from the UI entirely.** Still computed and
   still written on every apply (ticket 07 requires it), just never shown. It survives
   only in the prototype's Apply MessageBox, which stands in for the write log.
3. **"Feature off" vs "APE off" is now explicit**, not implied. Each feature carries a
   permanent state line — *"APE is applied. Unchecking puts h.sie's own 5–20% roll
   back"* vs *"Not applied — h.sie's own 5–20% roll is untouched"* — and setting hull
   to `0`–`0` raises its own line saying the crew repairs nothing while APE stays
   applied. The review list says the same thing in game terms.
4. **Per-line write targets are a distraction — removed.** The review list no longer
   names `sh3.exe` / `hsie.ini` or any address. It speaks only in game terms
   ("A diesel takes damage about 1.5 times per 10,000 hours of running, at any
   speed"), which also settles ticket 02's *group by feature, never by write target*
   without an exception.

**Art: wired in.** `data/art/BlinkingApe (OA modder).tga` (256×256, 24-bit RLE) is not
readable by GDI+, so it was converted once to `data/art/blinkingape.png` — flat grey
field keyed out by border flood-fill, then trimmed to content (187×251, portrait). It
is **embedded in the exe** at build time (`run.cmd` passes `/resource:...,ape.png`),
not loaded from disk, which is how the real single-exe tool should ship it too. It sits
top-left of C's verification header beside the product name; the header grew to 110px
to hold its portrait aspect. If the resource is missing the old placeholder box is
drawn instead, so the prototype still runs.

A and B stay only until C is folded into the real tool; then the whole directory moves
to a throwaway branch.

## Two simulation toggles in the bar

The two states hardest to reach in real life, and the ones most worth judging:

- **simulate unsupported build** — the v1 allowlist refusal (OAE 1.5.2 / h.sie
  `v 1.6E1` only, ticket 07).
- **simulate h.sie Diesel Damage flag ON** — h.sie's own switch is `0x00` in every
  surveyed install, so **disabled-with-explanation is the default state**, not an edge
  case (ticket 05). Leave it off to see what a real user sees first.

## Still live in C

- Try rate `0.5`: `p_dso` drops under the `2.98e-08` float32 lattice and C says so in
  plain words — the tuning range that only exists because APE rewrites h.sie's
  probability helper.
- Try band `0.30`–`0.90`: the "two unrepaired hits destroy it" figure jumps and the
  review line turns red. `0.15`–`0.55` gives **3.1%**, matching ticket 04.
- Hull `0`–`0` exercises the explicit feature-off wording added above.
