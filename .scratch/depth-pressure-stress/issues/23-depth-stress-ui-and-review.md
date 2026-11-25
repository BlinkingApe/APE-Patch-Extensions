# 23: "Depth stress" section and Review wording

**What to build:** the player sets depth stress up the same way as hull repair, diesel
damage and crush time. The UI gains a "Depth stress" section with one feature checkbox,
the tier-2 depth, and per tier the leakage, wear amount, wear rate and hull multiplier,
each in game units (m, % per game-minute, % per hit, game-minutes between hits, ×).
The tooltips say that:

- a class whose crush depth is shallower than the tier-2 depth goes straight from tier 1
  to tier 3 (IIB, XXIII);
- the stern compartment must be crewed for its leakage to be pumped;
- settings take effect at the next game launch.

The Review panel gains a depth-stress group that describes what will happen in game
terms. Until the `sh3.exe` load call site exists (ticket 02, umbrella map), the section
is shown disabled with an explanation, so no player deploys an `ape.act` that nothing
loads.

**Blocked by:** 22 (The patcher deploys, reads back and removes `ape.act`)

**Status:** ready-for-agent

- [ ] The section shows the installed values on reopen and writes the entered ones through ticket 22's session
- [ ] The tooltips for the tier-2 depth, leakage and next-launch timing say what the user stories ask
- [ ] The Review group describes tiers, leakage, wear and multiplier in game terms, and says an effect at its neutral value is off; `ReviewTests` cover it in both cultures
- [ ] While the load call site is missing, the section is disabled with an explanation, and that is gated by one switch that is easy to flip later
