// The review list: what a player reads before committing.

using System;
using System.Collections.Generic;
using System.Text;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class ReviewTests
    {
        static Review Build(PatchSession session, PatchState desired)
        {
            return Review.Build(session.Install, session.Plan(desired), desired);
        }

        static string AllText(Review r)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ReviewGroup g in r.Groups)
            {
                sb.AppendLine(g.Title);
                foreach (ReviewLine l in g.Lines) sb.AppendLine(l.Text);
            }
            if (r.NothingText != null) sb.AppendLine(r.NothingText);
            return sb.ToString();
        }

        public static void Test_ChangingNothingSaysSoOutright()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            Review r = Build(session, session.Current.Clone());

            Check.True(r.Empty, "no groups");
            Check.Equal(0, r.FeatureCount, "no features change");
            Check.True(!string.IsNullOrEmpty(r.NothingText), "an empty panel is never ambiguous");
            Check.Contains("Nothing", r.NothingText, "and it says so in words");
        }

        public static void Test_GroupsAreOnePerFeatureAndCounted()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState one = session.Current.Clone();
            one.HullApplied = true;
            one.HullMin = 10;
            one.HullMax = 30;
            Review r1 = Build(session, one);
            Check.Equal(1, r1.FeatureCount, "one feature changes");
            Check.Equal("Hull repair at sea", r1.Groups[0].Title, "and it is named");

            PatchState two = one.Clone();
            two.DieselApplied = true;
            Review r2 = Build(session, two);
            Check.Equal(2, r2.FeatureCount, "two features change");
            Check.Equal("Diesel damage", r2.Groups[1].Title, "grouped by feature, in order");
        }

        public static void Test_TheListNeverNamesAFileAnAddressOrAKey()
        {
            // The player is told consequences, not edits.  This is the invariant
            // the prototype round settled and it is worth holding by test.
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.HullMin = 10;
            want.HullMax = 30;
            want.DieselApplied = true;
            want.BandHi = 0.70;          // reach the warning line too
            want.Rate = 0.5;             // and the float32 note

            string text = AllText(Build(session, want));
            string[] forbidden = { "sh3.exe", "hsie.ini", "0x", ".pcode", ".pconst",
                                   "Val9", "Val10", "byte", "offset" };
            foreach (string bad in forbidden)
                Check.DoesNotContain(bad, text, "the review list stays in game terms");
        }

        public static void Test_TheDestroyThresholdIsLedWithNotThePercentage()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState safe = session.Current.Clone();
            safe.DieselApplied = true;
            safe.BandLo = 0.15;
            safe.BandHi = 0.50;
            string safeText = AllText(Build(session, safe));
            Check.Contains("One hit never destroys an engine", safeText,
                "the crew-repairable guarantee is stated unconditionally");
            Check.Contains("Two hits cannot destroy one either", safeText,
                "at 0.50 the threshold is stated, not a 0.0% figure");
            Check.DoesNotContain("%", CutRateLine(safeText),
                "no percentage is shown when there is nothing to warn about");

            PatchState risky = safe.Clone();
            risky.BandHi = 0.55;
            string riskyText = AllText(Build(session, risky));
            Check.Contains("Two hits on the same engine, the first still unrepaired", riskyText,
                "above the threshold the conditions are named");
            Check.Contains("3.1% of the time", riskyText, "and only then is a percentage shown");
            Check.Contains("cannot be repaired at sea", riskyText, "with what it costs");
        }

        public static void Test_OnlyThePercentageLineWarns()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState risky = session.Current.Clone();
            risky.DieselApplied = true;
            risky.BandHi = 0.55;
            Review r = Build(session, risky);

            int warnings = 0;
            foreach (ReviewGroup g in r.Groups)
                foreach (ReviewLine l in g.Lines)
                    if (l.Warn)
                    {
                        warnings++;
                        Check.Contains("Two hits on the same engine", l.Text,
                            "the destroy line is the one that warns");
                    }
            Check.Equal(1, warnings, "exactly one warning line");

            PatchState safe = risky.Clone();
            safe.BandHi = 0.49;
            foreach (ReviewGroup g in Build(session, safe).Groups)
                foreach (ReviewLine l in g.Lines)
                    Check.False(l.Warn, "below the threshold nothing warns");
        }

        public static void Test_TheIdleCutoffIsStated()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = session.Current.Clone();
            want.DieselApplied = true;
            Check.Contains("below one tenth telegraph count as idle", AllText(Build(session, want)),
                "the player is told the feature does not fire at all stop");
        }

        public static void Test_ZeroToZeroHullIsCalledOutAsAppliedButInert()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.HullMin = 0;
            want.HullMax = 0;

            Review r = Build(session, want);
            Check.Equal(1, r.FeatureCount, "it is a change");
            Check.Contains("repair nothing at sea", r.Groups[0].Lines[0].Text, "and it says so");
            Check.Contains("unchecking the feature instead", r.Groups[0].Lines[0].Text,
                "and distinguishes it from unchecking");
            Check.True(r.Groups[0].Lines[0].Warn, "and warns");
        }

        public static void Test_UncheckingSaysWhatComesBack()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState on = session.Current.Clone();
            on.HullApplied = true;
            on.HullMin = 40;
            on.HullMax = 60;
            on.DieselApplied = true;
            Check.True(session.Apply(on).Ok, "apply first");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.HullApplied = false;
            off.DieselApplied = false;

            string text = AllText(Build(applied, off));
            Check.Contains("h.sie's own 5 to 20% repair roll comes back", text,
                "hull says what returns");
            Check.Contains("h.sie's own damage model comes back", text,
                "and so does diesel damage");
        }

        public static void Test_AnUnreadableInstallReviewsAsNothingWritable()
        {
            // Ticket 13: with no whole-file identity check, the only way left
            // to land on Install.Supported == false is a missing or unreadable
            // file -- there is no "wrong build" case any more.
            string dir = Fixtures.TempDir();
            PatchSession session = PatchSession.Open(dir, Manifest.Embedded());
            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.DieselApplied = true;

            Review r = Build(session, want);
            Check.True(r.Empty, "nothing is listed");
            Check.Contains("could not read", r.NothingText, "and it says why");
            Check.True(r.NothingIsWarning, "in the warning colour");
        }

        public static void Test_TheDerivedStatsBlockLeadsWithTheThreshold()
        {
            PatchState d = new PatchState();
            d.Rate = 1.5;
            d.BandLo = 0.15;
            d.BandHi = 0.50;

            List<ReviewLine> safe = Review.DieselStats(d);
            Check.Equal("Mean damage per event   0.33 of the engine", safe[0].Text, "the mean");
            Check.Equal("One hit never destroys an engine.", safe[1].Text, "the guarantee");
            Check.Equal("Two hits cannot destroy one either.", safe[2].Text, "the threshold");
            Check.Equal("Wear rate               4.1667e-08 per game-second", safe[3].Text,
                "the wear rate, in the same form on any machine");
            foreach (ReviewLine l in safe) Check.False(l.Warn, "nothing warns below the threshold");

            d.BandHi = 0.55;
            List<ReviewLine> risky = Review.DieselStats(d);
            Check.Contains("Two hits on the same engine", risky[2].Text, "conditions named");
            Check.Contains("3.1% of the time", risky[3].Text, "then the percentage");
            Check.True(risky[2].Warn && risky[3].Warn, "and only that line warns");
        }

        /// <summary>The rate line legitimately carries no percent sign, but the
        /// band line's numbers must not be mistaken for one; strip the lines that
        /// are allowed to mention percentages before asserting.</summary>
        static string CutRateLine(string text)
        {
            List<string> kept = new List<string>();
            foreach (string line in text.Split('\n'))
                if (line.IndexOf("of maximum hull", StringComparison.Ordinal) < 0)
                    kept.Add(line);
            return string.Join("\n", kept.ToArray());
        }
    }
}
