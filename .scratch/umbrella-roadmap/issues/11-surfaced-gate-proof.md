# Prove the surfaced gate before shipping always-on diesel wear

Type: task
Status: resolved

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

`docs/re-notes/04-diesel-damage-signature.md` §7 records the identity of
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

## Update 2026-08-16 — static half exhausted, no answer either way

Ran the static pass this ticket calls for first: extended `chains.py`'s
`real_mem_refs` (which had a decode-abort bug undercounting hits — now fixed,
worth propagating back) to scan **every** section, not just `.text`, for
literal references to `[0x5543DC]`.

Result: **51 total sites** touch `[0x5543DC]` across `.text` and `.pcode`
(constructor `0x004A28B0`, destructor-path clear `0x004A2966`, plus 49 loads).
Of all 51, **exactly one** — the already-known diesel gate at `0x006350D0` —
touches `+0x18` or `+0x1C`. Nothing anywhere writes either field; nothing else
reads them.

The depth-handling region (`00514180+`/`00514560+`, per ticket 01's note) does
reference `[0x5543DC]`, but only as a null-guard at `00514560` — the actual
dereference it uses lands on **`+0x10`** (fed into the depth setter
`0x00513FC0` via `fabs`), not `+0x18`/`+0x1C`. So the region is confirmed
connected to this singleton, but doesn't narrow the two target fields.

Object-identity clues found: `[0x5543DC]`'s object is allocated (328 bytes,
`0x004A28B0`) and destroyed with mission lifecycle, rooted through the same
`[[0x5449B0]+0xD8]` manager as the depth code, zero-initialized uniformly
across `+0x10..+0x147` (so `+0x18`/`+0x1C` aren't distinguished at
construction), and a sanity-check routine (`0x0047C130`) exercises several
sibling float fields (`+0x10/+0x14/+0x24/+0x30/+0x34`) against epsilon
constants — consistent with an environment/physics-state object, not proof of
one.

**Conclusion: static evidence is circumstantial, not dispositive.** This is
exactly the fork the ticket anticipated — the static pass is genuinely
exhausted, not under-run, and the live half (watch `[0x5543DC+0x18]`/`+0x1C`
across surface → dive → submerged → surface, plus snorkeling, per
`docs/re-notes/01-live-session-playbook.md`'s method) is still required to
answer the question. The repairability check (§ "Also verify...") is
untouched — it also needs the live session, in-game.

## Update 2026-08-16 (2nd session) — live session run, trust broken, paused

Ran the CE session this ticket calls for. Summary of what happened, kept
honest rather than tidied up, because the failure mode matters for whoever
resumes this.

**What got confirmed, independent of any CE addressing:**
- The diesel-damage cave **is live and firing** in this install/session —
  full sequence observed in-game: warning message ("...risking a
  breakdown!"), then "Stb Diesel Engine Damaged", then the crew management
  screen showing the engine as damaged. This matches §2–§4 of
  `04-diesel-damage-signature.md` exactly, including that the hit landed on
  the starboard engine (engine B in the model).
- This happened after setting `0x63CF13 = 0xFF` (enable flag) live via CE and
  ordering Ahead Flank — consistent with the model's gates.

**What did not work, across the session, in order:**
1. `gate_a`/`gate_1c` (`[[0x5543DC]+0x18]`/`+0x1C`, single-dereference
   chain) resolved mechanically correctly in CE (base deref confirmed via
   `Find out what accesses 0x5543DC` matching known code addresses from the
   notes) and read a frozen, identical raw value (`0x86`) on both fields,
   unmoved across a surface→periscope-depth dive. Combined with the static
   pass's finding of zero writers to either offset anywhere in the binary,
   this was read as "dead field, gate always passes" — **not independently
   confirmed live**, since the session never got to a genuine submerged hit
   to test it against real behavior.
2. The engine-health chain was given wrong **twice**: first as flat address
   arithmetic (`5543DC+0x18` typed as static math, not a real dereference —
   crashed the game writing `7.5` into unrelated memory), then as
   `[[5543DC]+4]+0x30C` (two dereferences) — still wrong, read `7.5` then
   `-1.99e-33` then `7.2e22` across attempts, all nonsense for a 0.0–1.0
   field. A closer disasm read found two *more* real dereferences
   (`mov edi,[eax]` then `push [edi]` are both genuine memory reads, not a
   register-held float) — corrected chain never got tested live.
3. With the corrected 4-offset chain not yet verified, tried to ground-truth
   it directly: breakpoint at `006351E0` (`fstp dword [edx]`, the instruction
   that writes the clamped damage value). **The breakpoint did not fire**,
   despite a real, confirmed diesel-damage event landing moments later in
   the same run.

**The breakpoint miss is the important data point, not the pointer-chain
mistakes.** A `fstp` at that exact VA either executes on every damage hit or
the disassembly notes don't describe *this* running binary. Two chain-math
mistakes are explainable as agent error; a breakpoint on a static VA not
firing while the exact behavior it's supposed to gate is observed firing
anyway is much more consistent with **the live binary not matching the
build the RE notes were taken against**, and that was never checked this
session. `01-live-session-playbook.md` names the expected build
(`4B6BEE20`) and says explicitly "if the SHA-256 no longer matches, the
anchors below may have moved" — that check was skipped going into this
session.

**Before resuming:**
- Hash the running `sh3.exe` and compare against the build the diesel-damage
  disassembly (`04-diesel-damage-signature.md`) and this ticket's addresses
  were derived from. If it doesn't match, every absolute VA in this ticket
  (`0x5543DC`, `0x6350CA`, `0x6351E0`, etc.) needs re-establishing against
  the actual running binary before any more CE time is spent.
- If the hash *does* match, the breakpoint miss needs its own explanation
  (wrong breakpoint type for that memory region, code executing from a
  different mapped copy, off-by-something in the VA) before trusting any
  address in this note again.
- The mechanism-level finding (cave fires, gates as modeled, message/target
  sequence matches) is solid and doesn't need re-litigating. The
  address-level findings in this ticket (gate identity, engine-health
  location) are **not yet trustworthy** and shouldn't be cited as resolved
  until re-verified against a confirmed-matching binary.

Status left as `claimed`, not resolved. The core question (does the
surfaced/gate check actually block submerged diesel wear) is still open.

## Answer — 2026-08-16 (3rd session) — resolved by scope pivot, not by RE proof

The user proposed unsticking this ticket on a new premise: stop trying to prove
the gate's and the damage model's mechanics from first principles, trust that
h.sie's developer implemented them as advertised, and instead prove something
narrower and directly testable — that **APE's own patch controls the two
levers it actually writes** (damage magnitude, and timing/rate), independent
of the gate-identity debate.

**Accepted, with the risk named explicitly rather than dismissed.** This is a
real risk acceptance, not a formality: the 2nd live session found
`[0x5543DC+0x18]`/`+0x1C` reading a frozen, identical raw value across a
surface→periscope-depth dive, which is *suggestive* of a dead/always-pass
gate — not confirmed, because that session's binary hash was never checked
against the RE notes, but not nothing either. Two more rounds of RE spend
(static was exhausted this session's first pass; live broke on a breakpoint
that didn't fire) have not resolved it, and the map's own method notes now
flag this specific ticket as the case study for "hash the exe before trusting
CE." Diminishing returns, not a dead end that must be pushed through.

**Resolution:** close this ticket without establishing the gate's identity.
The mechanism-correctness question (does "surfaced" really gate submerged
wear) is set aside on trust, not answered. In its place,
[ticket 12](12-diesel-levers-blackbox-test.md) becomes the actual pre-ship
gate for diesel damage: a deterministic in-game test (damage band forced to
1.0–1.0, rate forced high enough to land within ~10 in-game minutes, boat
underway at any nonzero telegraph) that proves APE controls magnitude and
timing regardless of how the surfaced-gate mechanics actually work. It also
folds in this ticket's still-open repairability-at-exactly-1.0 question for
free, since the forced magnitude *is* 1.0.

The gate-identity question itself is not deleted — it's carried forward in
the map's Not yet specified as an accepted, named risk, in case ticket 12 or
later play surfaces evidence that reopens it (e.g., damage appears to accrue
with the boat provably submerged and non-snorkeling).
