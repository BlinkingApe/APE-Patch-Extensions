# Prove the surfaced gate before shipping always-on diesel wear

Type: task
Status: open

## Blocked by

None. (Depends on nothing; ticket 07 should not put diesel damage in the v1 list
until this resolves, since a negative result redesigns the feature.)

## Question

Raised by the 2nd-pass resolution of `04-diesel-damage-patch-scope.md`. The
diesel cave gates on this, at `0x6350CA`:

```
006350CA  d9 05 64c46300   fld   dword [0x63C464]     ; 0.0
006350D0  a1 dc435500      mov   eax, [0x5543DC]
006350D5  d9 40 18         fld   dword [eax+0x18]
006350D8  d8 40 1c         fadd  dword [eax+0x1c]
006350DB  df f1            fcomip st(1)               ; sum vs 0.0
006350DF  0f 82 1f010000   jb    0x635204             ; sum < 0 -> skip the tick
```

`docs/re-notes/04-diesel-damage-signature.md` §6 records the identity of
`[0x5543DC+0x18]` and `[+0x1C]` as **not established**. The obvious reading is a
depth/surfaced test — diesels only run on the surface — but nothing in the
disassembly proves it.

**Why this now matters more than it did.** Under h.sie's shipped model the
healthy case was `MTS^200`, mathematically inert below telegraph 0.97, so a
faulty surface gate was almost invisible: you would have to be submerged at very
near flank to notice. Ticket 04 has flattened the exponent to `0`, making the
risk **speed-independent and continuously live**. If this gate does not actually
mean "surfaced", the mod will damage diesels while the boat is submerged and
running on electrics — at every depth, at every speed, for the whole patrol.

This is the one finding that can invalidate ticket 04's resolution. If the gate
is not a surface test, the feature needs a real surfaced condition, which is not
expressible in `hsie.ini` and would reopen both the `ape.act` question and the
ticket 10 boundary that ticket 04 just closed.

Establish, in rough order of cost:

- **Static first**, per the map's method note. Find other readers of
  `[0x5543DC+0x18]` / `[+0x1C]` across stock `.text` — `0x5543DC` has 55 stock
  xrefs — and see whether any of them sits in code with an obvious depth,
  buoyancy, or ballast meaning. Two floats summed and tested against zero is a
  suggestive shape (fore/aft ballast? trim? two tank groups?).
- **Then live**, using the session playbook from ticket 01. Watch both floats
  across surface → dive → submerged → surface, and confirm the sum crosses zero
  at the waterline and nowhere else. Also check snorkelling, which is the
  interesting edge: the boat is submerged but the diesels *are* running, so
  whatever this gate does decides whether snorkel time counts as wear.
- **Record the snorkel answer either way** — it is a design question ticket 04
  did not consider, and the historian's framing ("anytime diesels are in use")
  arguably wants snorkel time included.

## Also verify in the same session: is diesel damage repairable?

Added 2026-08-15 with ticket 04's damage-band decision, which rests entirely on
this and does not prove it.

The historian's constraint is that damage **should be repairable by the crew on
the boat**. Ticket 04 encodes that as "keep the damage float below `1.0`", on the
assumption that below 1.0 the crew can repair it and at 1.0 they cannot — standard
SH3 behaviour, and consistent with h.sie's own messaging (string 4893 fires on
partial damage only, never on destruction). **It has not been checked for this
field.**

While the boat is on the surface with a damaged diesel, confirm:

- the crew can be assigned to repair `[[0x5543DC]+4]+0x30C` back toward `0.0`,
  and the float actually falls;
- whether a value of exactly `1.0` is repairable at sea or needs base repair;
- roughly how long a repair takes, since the "Realistic Repair Times" patch
  (`Val1 = 60.0`) multiplies it and the two interact.

**If repair does not clear the field**, ticket 04's band decision needs revisiting:
damage would accumulate monotonically across a career, and even `0.15…0.55`
eventually destroys — three unrepaired hits reach `1.0` about 59% of the time.

Resolved when the gate's meaning is established and ticket 04's resolution is
either confirmed or sent back for a surfaced condition.
