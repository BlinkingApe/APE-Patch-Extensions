# 32: Does `[0x63E1D4]` track the ordered telegraph on OAE while submerged?

**What to find:** motor flooding damage (ticket 31) treats "running" as "the ordered
machine telegraph is not at stop". The only known read is `[0x0063E1D4]`, which h.sie's
diesel cave refreshes per tick (`docs/re-notes/04-diesel-damage-signature.md` §4). It was
found for the diesels and has never been watched on OAE while submerged on electric.
Hash `sh3.exe` first (`docs/re-notes/01-live-session-playbook.md`).

**Blocked by:** none

**Status:** resolved (live on the OAE lab, 2026-11-25; no code change needed)

- [x] On OAE, submerged, `[0x63E1D4]` follows every telegraph order: stop, each ahead step, each astern step
- [x] It reads exactly 0.0 at stop, and astern's encoding is recorded (needed for "astern counts as running")
- [x] Also checked surfaced, and across a save-load
- [x] If it doesn't track submerged, a replacement read is found or the gap is filed against ticket 31 (n/a: it tracks)

## Answer

**Yes.** On OAE 1.5.2 (`sh3.exe` `20360fe8…`), submerged on electric at 30 m, surfaced,
and after a save-load, `[0x63E1D4]` follows every telegraph order. It always equals its
source, `[[[[0x5543DC]+4]+0x320]]`, one refresh (~1 s) or less behind. Stop is exactly
`0x00000000`. Astern is a plain negative float: −0.26 / −0.40 / −0.53 / −0.66 on the
Type IIA the lab spawns. Ahead is 0.40 to 1.0. So ticket 31's gate (`telegraph != 0.0f`,
plausible in [-1, 1]) is right as built, and astern counts as running.

After a load the copy reads 0 for about a second while the source already holds the
saved order. APE sees stop, so it skips at most a second of motor flooding damage and
never adds any.

Values, lags, the save-load trace and a keyboard trap for the next live hunt are in
`docs/re-notes/08-oae-port-of-depth-leads.md` §9. A test pins every measured value
against the gate ("motor: the telegraph values measured on OAE").

## Comments

2026-11-25: the agent ran this on the sh3-crush-pressure OAE lab (`SH3_LAB=oae`, the
robocopy, never the owner's install), driven through `harness.drive` with a Frida
logger attached at the main menu. Probes, raw rows and screenshots are in that repo.
Both `sh3.exe` were hashed in the session shell before and after the run (`20360fe8…`
both times); those hashes aren't stored in the rows. The lab ledger was verified
unchanged. The session window closed and restored `Documents\SH3`, so the test save is
gone.

The first submerged sequence's three stop orders never reached the game (the trap in
note 08 §9). Submerged stop was then proven separately, from ahead flank, back
emergency and ahead slow.
