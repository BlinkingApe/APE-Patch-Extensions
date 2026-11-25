# 25: Seepage

**What to build:** while the player boat is in a depth tier, water rises in its
sternmost compartment at the tier's leakage rate, up to 0.8 flood, for the crew to pump.
The reader walks the zone list and picks the seep compartment at run time from the
zone→compartment table: the zone that maps to compartment 8 if there is one, else the
one that maps to 4 (ticket 19). It never decides by class, so U-boats added by mods are
covered too. The step adds `rate·dt` when in a tier with leakage > 0 and dt > 0. It
writes nothing at or above 0.8, clamps to 0.8, and never lowers the flood. The writer
writes the flood float, and the log shows old → new.

**Blocked by:** 24 (Hull multiplier, first end-to-end slice)

**Status:** done in code (2026-11-25); in-play check is ticket 27 test 2

- [x] The compartment choice is made from the live table, with the zone-list anchor's AOB recorded; no matching zone means no sample for seepage and a logged reason
- [x] Step tests: steps by `rate·dt`; clamps at 0.8; no write at or above 0.8; never lowers; no seep above 100 m, when paused (dt 0) or when the clock goes back; leakage 0 turns it off without affecting the other effects
- [x] Step tests: seepage continues at tier-2 values below crush depth
- [ ] Injected into OAE, an undamaged boat at tier-1 depth takes water in the expected compartment, and the water stops rising above 100 m and isn't removed by APE (in-play test 2, informally)

## Notes (2026-11-25)

Built: `Sample` gained `seep_flood`/`has_seep`; `step()` writes `Flood` as `min(flood +
rate·dt, 0.8)`, nothing at or above 0.8. `ape.act` scans the zone-list head and the
zone→compartment table AOBs (`docs/re-notes/09`), walks the list and picks compartment 8's
zone, else 4's. No matching zone leaves `has_seep` false and logs `no seepage: <why>`.
The last criterion (injected into OAE) is still to do, under ticket 27.
