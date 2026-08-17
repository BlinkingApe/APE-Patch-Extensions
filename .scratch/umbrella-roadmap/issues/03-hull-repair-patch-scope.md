# Hull-repair patch: scope

Type: grilling
Status: resolved

## Blocked by

02 (stretch goal only — the shared `ape.cfg` strategy gates the *configurable
amount* half of this ticket, not the base "what does disabling hull repair
mean" decision, which is answerable independently)

## Question

Goal 2: patch the hsie/stiebler u-tanker mod's hull-repair behavior so hull is
no longer repaired, while every other function of that mod keeps working.
Stretch goal: make the amount of hull repair settable via `ape.cfg`, using
the config strategy decided in ticket 02 (shared ape.cfg strategy across
subsystems).

Pin down enough to spin up its own `.scratch/hull-repair-patch/` feature
effort (PRD + issues, same shape as `crush-depth-damage`):

- Exact behavior when disabled: hull repair amount forced to zero, the
  repair function no-op'd/hooked out entirely, or something else? What
  "every other function keeps working" excludes (list the u-tanker mod's
  other functions so the boundary is explicit).
- Whether this is a config-off-by-default toggle or hull-repair is always
  disabled with the stretch goal only adjusting the *amount* (i.e. is "off"
  itself ever configurable, or only the repair rate when not fully off).
- Whether locating the relevant function is RE work in the same style as
  crush-depth-damage's AOB-anchor hunt (`.scratch/depth-pressure-stress/issues/01-*`)
  — if so, note it as the first issue of the new feature effort rather than
  resolving it here. Starting method for that hunt:
  `docs/re-notes/02-hsie-patch-identification-method.md` (jump-away site +
  patch-enable flag-table breakpoint technique, contributed by a debugger
  experienced with h.sie/Stiebler's `.act` patches) — the u-tanker hull-repair
  logic is a hsie/Stiebler patch, so expect it to live in a jump-away code cave
  in `hsie.act` rather than inline in `sh3.exe`. That RE pass also determines
  whether "hull repair forced to zero" is better done as a runtime write
  (ape.act-style) or a static on-disk edit to `hsie.act` (patch1337-style,
  same doc, last section) — worth deciding once the site is found, not before.

## Answer

Resolved 2026-08-15. The RE half is **done, not deferred** — a new static method
(diffing per-patch `sh3.exe` variants with `pefile`+`capstone`, no debugger)
located the site outright, so this ticket does *not* spin up an AOB-hunt issue.
Full write-up: `docs/re-notes/03-utanker-hull-repair-signature.md`.

### The premise this ticket was written on was wrong

h.sie's patches are **not** in `hsie.act`. They live in four sections appended to
`sh3.exe` itself (`.pcode` 0x633000, `.pconst` 0x63C000, `.pdata` 0x63E000, a
second `.text` 0x640000). The enable/disable flag table is at VA `0x63CF00`, one
byte per patch, `0xFF`/`0x00` — U-Tanker is `0x63CF0C`, Diesel Damage `0x63CF13`.
`docs/re-notes/02-hsie-patch-identification-method.md` has been corrected.

### What the U-Tanker mod does (the explicit boundary this ticket asked for)

Gate: cave at `0x633743` matches the mission-order string `"Dock at: U-"` /
`"Anlegen in: U-"` and sets `[0x63E168] = 1`. The resupply cave at `0x6335A1`
(hooked by `jmp` at `0x00455CB4`) then performs **three independent actions**:

1. **Block 1 @ `0x6335FA`** — `[boat2+0x360] += 0.30 x [boat2+0x358]`, clamped.
   A second scalar resource; **fuel is inference, not proof**.
2. **Block 2 @ `0x633633`** — **hull repair**:
   `hull += (rand(0..15) + 5) x 0.01 x maxHull`, clamped.
   Writes `[boat+0xB8]` — the same hull-HP field `01-crush-depth-damage-signature.md`
   derived independently from the HUD mirror. This is the target.
3. **Block 3 @ `0x63368D`** — torpedo reload, fills up to 4 empty slots with
   `rand(0..3)+1` torpedoes.

Plus four localised messages. **"Every other function keeps working" = blocks 1
and 3 and all messages untouched.** Only block 2 is in scope.

### Decisions

- **Not a hard off-switch.** Users set the repair **range** themselves; "off" is
  the bottom of that range (min = max = 0), not a separate mechanism. So the edit
  is to **narrow the roll** — the `push 0x10` (width) and `add eax, 5` (floor)
  operands — rather than branch over the block. Skipping the block stays the
  fallback if a true zero must not touch `[boat+0xB8]` at all.
- ⚠ **Do not touch `0x63C404`** (the `0.01`). It is a generic shared constant,
  not private to this patch.
- **Delivery: a static GUI patcher, not `ape.cfg`.** APE ships a patcher `.exe`
  with a GUI in the style of hsieOptions — a checkbox per patch plus input values
  for patches that take them. Checkboxes drive the `0x63CF00` flag table directly;
  value inputs drive constants and operands like this one. This is a **new APE
  deliverable** alongside `ape.act` — see the patcher scope ticket (05).
- **`ape.cfg` is dropped entirely** (see the rewritten ticket 02). Every APE
  setting becomes a static byte written by the patcher. This **reverses ADR-0004**
  and retires `src/core/config.{hpp,cpp}`, which is shipped crush-depth code —
  chosen with that cost stated. Note the reading being carried forward: dropping
  `ape.cfg` drops the runtime *config file*, **not** `ape.act`'s runtime logic —
  crush-depth damage is computed live and cannot become a static patch, so the
  patcher must bake its constants into `ape.act` itself. Ticket 06 resolves how.

### Ready to spin up `.scratch/hull-repair-patch/`?

**Not yet — and the reason is a scope shift.** Everything specific to hull repair
is settled, but the deliverable is no longer a standalone mod: it is one checkbox
plus two value inputs inside a patcher that does not exist yet. Sequence the
patcher's scope (05) and its ape.act value-delivery question (06) first; hull
repair then becomes an early consumer that proves the tool end-to-end.

### Carried forward as caveats

- The three variant exes are a **sibling build** — 1,417,216 bytes but none
  matching registered build `4B6BEE20`. Verify against an install binary.
- VAs recorded here are absolute from one variant set; ADR-0003 wants wildcarded
  AOB signatures with a uniqueness proof. Not yet done.
- Flag `0x63CF05` also reads the docking state, so "the U-Tanker mod" may span
  more than one flag byte.
