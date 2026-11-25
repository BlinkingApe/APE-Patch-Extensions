// "What will change", in game terms.
//
// The review list is the tool's central claim to the player: it says what their
// settings will do, not what bytes will move.  So it names no file, no address
// and no ValN key -- a decision taken in the prototype round and kept here --
// and it is grouped by feature, because a feature is what a checkbox owns.
// The one exception is the crush-time patch, which names every .zon it writes:
// its targets are discovered per install, and the player has to see which
// classes they are (depth-pressure-stress ticket 16).
//
// It lives in the core rather than the form for the same reason the arithmetic
// does: it is the part worth testing, and the WinForms layer only lays it out.

using System.Collections.Generic;

namespace ApePatcher
{
    public class ReviewLine
    {
        public string Text;
        public bool Warn;

        public ReviewLine(string text, bool warn) { Text = text; Warn = warn; }
    }

    public class ReviewGroup
    {
        public string Title;
        public List<ReviewLine> Lines = new List<ReviewLine>();
    }

    public class Review
    {
        public List<ReviewGroup> Groups = new List<ReviewGroup>();

        /// <summary>How many features will change. Zero is the case the panel has
        /// to state outright, so an empty list is never ambiguous.</summary>
        public int FeatureCount;

        /// <summary>Shown instead of the groups when nothing will change.</summary>
        public string NothingText;

        public bool NothingIsWarning;

        /// <summary>Shown under the groups when something will.</summary>
        public string Footnote =
            "Nothing else is touched, including any hsieOptions choices.";

        public bool Empty { get { return Groups.Count == 0; } }

        // h.sie's own hull roll, stated in the words the player sees.
        const string HsieHullRoll = "5 to 20%";

        public static Review Build(InstallReport install, WritePlan plan, PatchState desired)
        {
            Review r = new Review();

            if (!install.Supported)
            {
                r.NothingText = "Nothing — APE could not read this install.";
                r.NothingIsWarning = true;
                return r;
            }

            if (plan.Touches(Manifest.HullPatchId)) r.Groups.Add(Hull(desired));
            if (plan.Touches(Manifest.DieselPatchId)) r.Groups.Add(Diesel(desired));
            if (plan.Touches(CrushTime.PatchId)) r.Groups.Add(Crush(install, plan, desired));
            r.FeatureCount = r.Groups.Count;

            if (r.Groups.Count == 0)
                r.NothingText = "Nothing. Every feature is left exactly as this install "
                    + "already has it.";
            return r;
        }

        /// <summary>Why a class the crush-time patch will not touch is left alone,
        /// or null for one it can write. Shared with the panel's own note.</summary>
        public static string LeftAlone(UBoatZon b)
        {
            if (b.State == ZonState.ChangedByOtherMod)
                return b.FileName + " was changed by another mod after APE wrote it. APE leaves "
                    + "it alone.";
            if (b.State == ZonState.Unreadable)
                return "APE cannot read " + b.FileName + " safely (" + b.Problem + "). APE "
                    + "leaves it alone.";
            return null;
        }

        /// <summary>The one group that names files: the player has to be able to
        /// see which classes, and which shared AI boats, a crush time reaches
        /// (ticket 16).</summary>
        static ReviewGroup Crush(InstallReport install, WritePlan plan, PatchState d)
        {
            ReviewGroup g = new ReviewGroup();
            g.Title = CrushTime.PatchName;

            if (!d.CrushApplied)
            {
                g.Lines.Add(new ReviewLine("Each U-boat's own crush speed comes back, from the "
                    + "copy APE kept beside its .zon file.", false));
                foreach (ZonWrite w in plan.ZonWrites)
                    g.Lines.Add(new ReviewLine("Restores " + w.Boat.RelativePath + ".", false));
            }
            else
            {
                g.Lines.Add(new ReviewLine("Below crush depth, a U-boat at full hull is crushed in "
                    + "roughly " + Fmt.Int(d.CrushTime) + (d.CrushTime == 1 ? " second" : " seconds")
                    + ", whatever its class: a little longer just past crush depth, about a third "
                    + "as long at its rated crush depth.", false));
                foreach (ZonWrite w in plan.ZonWrites)
                    g.Lines.Add(new ReviewLine("Writes " + w.Boat.RelativePath + ": "
                        + Fmt.F1(w.Boat.Record.HitPoints) + " hull ÷ " + Fmt.Int(d.CrushTime)
                        + " s = " + Fmt.F2(w.CrashSpeed) + " hull per second.", false));
                g.Lines.Add(new ReviewLine("AI U-boats of these classes change too, because they "
                    + "share the file.", false));
            }

            foreach (UBoatZon b in install.UBoats)
            {
                string why = LeftAlone(b);
                if (why != null) g.Lines.Add(new ReviewLine(why, true));
            }
            g.Lines.Add(new ReviewLine("The game reads this when a mission loads, so it takes "
                + "effect from the next mission you start or load.", false));
            return g;
        }

        /// <summary>The derived-stats block under the diesel fields. Threshold
        /// first; the percentage appears only when it is non-zero, and only with
        /// its conditions named, because 3.1% is conditional on two hits landing
        /// on the same engine with the first still unrepaired -- it is not a
        /// per-patrol risk figure.</summary>
        public static List<ReviewLine> DieselStats(PatchState d)
        {
            List<ReviewLine> lines = new List<ReviewLine>();
            lines.Add(new ReviewLine("Mean damage per event   " + Fmt.F2(d.MeanDamage)
                + " of the engine", false));
            if (d.OneHitNeverDestroys)
                lines.Add(new ReviewLine("One hit never destroys an engine.", false));
            if (!d.TwoHitsCanDestroy)
                lines.Add(new ReviewLine("Two hits cannot destroy one either.", false));
            else
            {
                lines.Add(new ReviewLine("Two hits on the same engine, the first", true));
                lines.Add(new ReviewLine("still unrepaired, destroy it "
                    + Fmt.F1(d.PercentTwoHitsDestroy) + "% of the time.", true));
            }
            lines.Add(new ReviewLine("Wear rate               " + Fmt.Sci(d.Pdso)
                + " per game-second", false));
            return lines;
        }

        static ReviewGroup Hull(PatchState d)
        {
            ReviewGroup g = new ReviewGroup();
            g.Title = "Hull repair at sea";

            if (!d.HullApplied)
            {
                g.Lines.Add(new ReviewLine("h.sie's own " + HsieHullRoll + " repair roll comes "
                    + "back. APE's change is taken out.", false));
                return g;
            }

            if (d.HullRepairsNothing)
            {
                g.Lines.Add(new ReviewLine("The crew will repair nothing at sea. APE stays "
                    + "applied — unchecking the feature instead gives h.sie's "
                    + HsieHullRoll + " roll back.", true));
                return g;
            }

            g.Lines.Add(new ReviewLine("The crew repairs " + Fmt.Int(d.HullMin) + " to "
                + Fmt.Int(d.HullMax) + "% of maximum hull each time they succeed, instead of "
                + "h.sie's " + HsieHullRoll + ".", false));
            return g;
        }

        static ReviewGroup Diesel(PatchState d)
        {
            ReviewGroup g = new ReviewGroup();
            g.Title = "Diesel damage";

            if (!d.DieselApplied)
            {
                g.Lines.Add(new ReviewLine("h.sie's own damage model comes back — his rate, "
                    + "his two damage bands, and his flank-only speed curve.", false));
                return g;
            }

            g.Lines.Add(new ReviewLine("A diesel takes damage about " + Fmt.F1(d.Rate)
                + " times per 10,000 hours of running, at any speed.", false));
            g.Lines.Add(new ReviewLine("Each hit costs " + Fmt.F2(d.BandLo) + " to "
                + Fmt.F2(d.BandHi) + " of one engine.", false));

            // Lead with the threshold, because it is the fact that decides the
            // outcome; show the percentage only when it is non-zero, and only
            // with its conditions named.
            if (d.OneHitNeverDestroys)
                g.Lines.Add(new ReviewLine("One hit never destroys an engine, so the crew can "
                    + "always repair it.", false));
            if (!d.TwoHitsCanDestroy)
                g.Lines.Add(new ReviewLine("Two hits cannot destroy one either.", false));
            else
                g.Lines.Add(new ReviewLine("Two hits on the same engine, the first still "
                    + "unrepaired, destroy it " + Fmt.F1(d.PercentTwoHitsDestroy)
                    + "% of the time — and a destroyed engine cannot be repaired at sea.",
                    true));

            g.Lines.Add(new ReviewLine("Diesels below one tenth telegraph count as idle and "
                + "take no wear.", false));

            if (d.UnderFloat32Epsilon)
                g.Lines.Add(new ReviewLine("That rate is finer than the game could otherwise "
                    + "resolve. It works only because APE replaces h.sie's probability "
                    + "helper.", false));
            return g;
        }
    }
}
