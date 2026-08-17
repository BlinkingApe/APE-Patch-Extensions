# SH3 flooding model — static pass (ticket 10, depth-pressure-stress)

Target for `.scratch/depth-pressure-stress/issues/10-sh3-flooding-model.md`. For a
one-glance status of every depth-pressure-stress live value (not just this one),
see `01-crush-depth-damage-signature.md`'s "NEXT SESSION — START HERE" table.

**Static
only, one pass, 2026-08-17.** This ticket started from zero — "no anchor and no
offset exist for this today" — so this note begins there too: two real leads found,
neither confirmed. The mechanism decision, the value anchor, and the target
compartment all remain HITL; nothing here should be read as resolving them.

**Build tested:** `C:\games\OneAlex Extended 1.5.2\sh3.exe`, sha256
`eeef8595a75d2c95d768867f7c3eddea48b7940bd3d82ca2d159857476c68b6` — unchanged from
the two most recent depth-pressure-stress static passes
(`01-crush-depth-damage-signature.md` §7–§8), no drift this session.

**Method:** same as `01-crush-depth-damage-signature.md` §5 (`pefile` + `capstone`,
extended ad hoc for this pass, not yet folded back into `tools/re-static/`), but run
**strings-first rather than disassembly-first** — this ticket, unlike 08/09/11, had
no known field to trace outward from, so the cheapest entry point was compartment
naming/UI data, not code.

---

## 1. Compartment naming — SH3 exposes at least two independent schemes

`data/Menu/en_menu.txt` carries two unrelated compartment enumerations, neither of
which is a numeric index table in code:

- **Crew-station list, string IDs 245–256:** `bridge, sonar radio, command room,
  diesel engines, electric engines, bow torpedo, bow quarters, stern quarters,
  stern torpedo, deck casing, flak, damage control team`. Diesel and electric
  engines are their own stations here, distinct from `stern quarters`/
  `stern torpedo` — "the engine room" is not a single entry.
- **Damage-control equipment list, string IDs 458–496:** includes `458=Main Pump`,
  `460/461=Stb/Port Diesel Engine`, `462/463=Stb/Port Electric Engine`,
  `468=Aft Batteries`, `470=Stern Dive Planes`, `471–474=Rudder/Propeller
  Port/Stb`, `496=Stern tubes`.
- **Camera-station table, string IDs 3700–3727:** `3703=COMMAND ROOM;InteriorFore`
  / **`3704=DIESEL ENGINE ROOM;InteriorAft`** — SH3's own interior model names its
  sternmost renderable compartment the diesel engine room, and tags it
  `InteriorAft`.

**Reading:** the `InteriorAft` tag is concrete evidence (not inference) favoring the
engine room over the stern torpedo room for "astern" — consistent with the
historian's stuffing-box reasoning (ticket 10's Question, sub-question 3). It does
not settle *which* engine room, since diesel and electric engines are tracked as
distinct stations/equipment elsewhere in the same data.

No numeric compartment-index enum was found anywhere in code or data. The `.zon`
file (per-boat hitbox/armor geometry) is binary — `Spheres`/`Boxes`/`ArmorLevel`/
`LinkTo` — and carries no compartment names or flooding fields.

## 2. A read/compare site for flooding — lead, not an anchor

Searched `.text`+`.pcode` for `push` of the flooding-message string IDs (592–594,
563 — "We have flooding, sir!" / "destroyed by flooding" family). All hits are
single-occurrence and land inside one function, `0x435B30` (`this = esi`), which
walks a linked list of compartment/subsystem nodes (`next` at `[node+0x2C]`) rooted
through `[0x5449B0]→[+0xD8]→[+0x83C]` — the "god"/manager object already on record
in `01-crush-depth-damage-signature.md` §2/§6. **This is a different root from
`0x5543DC`**, which does not appear anywhere in this function's ~0x1500-byte body —
`0x5543DC` is confirmed to be engine-specific (the diesel/telegraph object,
`04-diesel-damage-signature.md`), not the general damage-control root.

At `0x436BE4`, a float `[ecx+0x1C]` is compared against the constant `0.35`
(`0x52058C`) to gate firing string 592 ("flooding, sir!"):

```
; inside 0x435B30, compartment-list walk rooted at [[[0x5449B0]+0xD8]+0x83C]
0x436BE4  d9 41 1c        fld   dword [ecx+0x1c]
          d8 1d 8c05... ? fcomp dword [0x52058c]      ; 0.35
          ...             jbe/ja -> string 592 ("We have flooding, sir!")
```

**Not confirmed:** which object `ecx` is at that point — a per-compartment node
walked from the list, or something else — was not resolved this pass; the enclosing
function is large and not every branch into that block was traced. The shape
(threshold-compare gating a message) matches the pattern `01-crush-depth-damage-
signature.md` §1 already trusts for hull-HP/depth code, which is why it's reported
as a lead rather than discarded, but **`ecx`'s identity is the open question**, and
until it's resolved this is not usable as a write target.

**No evidence either way on mechanism** (direct write vs. damage-derived) — this
site is a read/compare, not a write, so it doesn't distinguish the two.

## 3. h.sie's `.pcode` caves — negative

Scanned all printable ASCII strings in `.pcode` for `flood`/`pump`/`bilge`/`leak`/
`seep`/`water`: **zero hits**. No h.sie patch in this install touches flooding,
water, or pumps — unlike hull repair (§7 of the crush-depth note) or diesel damage
(`04-diesel-damage-signature.md`), there is no existing mod to borrow a mechanism
from here. `.pconst`/`.pdata`/`.rdata` turned up one incidental hit, the literal
string `"MainPump"` at `0x52E714` — a stock equipment-type-name buffer used in a
`rep stosd` load-time init pattern, not a runtime flooding value and not h.sie's.

## 4. Crew pump rate — nothing found

No numeric pump-rate value or rate constant was located this pass. Given the
ticket's own priority ordering (this sub-question is explicitly lower-priority than
the mechanism/anchor/compartment questions), it wasn't chased further.

## 5. What this leaves for ticket 10 (still fully HITL)

Two concrete, cheaper-than-a-blind-scan leads for the live session:

1. **Watch `[ecx+0x1C]` at `0x436BE4`** (inside `0x435B30`) while a compartment
   floods, to identify what `ecx` resolves to and confirm or deny it's a writable
   per-compartment flood float, and whether the compartment-list walk it sits in
   is a promising root for a static AOB later.
2. **The `InteriorAft`/"DIESEL ENGINE ROOM" finding narrows "which compartment is
   astern"** toward the engine room(s) — confirm against the damage-control
   screen's actual flooded-compartment label during a real depth-charge hit, and
   resolve whether it's the diesel engine room, the electric engine room, or both
   read together.

Everything else — the mechanism choice (direct write vs. damage-derived), the exact
value anchor, and crew pump rate — is unchanged from the ticket's original framing:
genuine discovery, not confirmation. This pass moves the ticket from zero anchors to
two open leads; it does not resolve it.
