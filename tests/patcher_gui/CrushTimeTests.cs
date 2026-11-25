// The crush-time patch (depth-pressure-stress ticket 16, ADR-0010): one player
// crush time becomes CrashSpeed = Hit_Points / crush time in every playable
// U-boat class's .zon, with a pristine .ape-orig copy kept for the revert.
//
// Like the other session tests, these go through PatchSession only: open a
// prepared game directory, apply, and assert what the files became.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class CrushTimeTests
    {
        /// <summary>A stock install with three playable classes and two AI ones,
        /// one of them with no HumanPlayable key at all.</summary>
        static Fixtures.Install WithBoats()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            Fixtures.AddUBoat(fx, "NSS_Uboat7c", "YES", 500f, 0.08f);
            Fixtures.AddUBoat(fx, "NSS_Uboat2A", "YES", 400f, 0.08f);
            Fixtures.AddUBoat(fx, "NSS_Uboat21", "NSS_Uboat21.CFG", "yes", 600f, 0.03f);
            Fixtures.AddUBoat(fx, "NSS_Uboat14", "NO", 300f, 2f);
            Fixtures.AddUBoat(fx, "NSS_Uboat2B", null, 150f, 2f);
            return fx;
        }

        static string ZonOf(Fixtures.Install fx, string folder)
        {
            return Path.Combine(fx.Dir, "data", "Submarine", folder, folder + ".zon");
        }

        static PatchState Crush(PatchSession session, int seconds)
        {
            PatchState s = session.Current.Clone();
            s.CrushApplied = true;
            s.CrushTime = seconds;
            return s;
        }

        // -- discovery ---------------------------------------------------------

        public static void Test_OnlyHumanPlayableClassesAreFound()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();

            Check.Equal(3, session.Install.UBoats.Count, "the three HumanPlayable=YES classes");
            Check.Equal("NSS_Uboat21", session.Install.UBoats[0].Folder, "sorted by folder");
            Check.Equal("NSS_Uboat2A", session.Install.UBoats[1].Folder, "sorted by folder");
            Check.Equal("NSS_Uboat7c", session.Install.UBoats[2].Folder, "sorted by folder");
            Check.Equal("data/Submarine/NSS_Uboat7c/NSS_Uboat7c.zon",
                session.Install.UBoats[2].RelativePath, "named relative to the game folder");
            Check.True(session.Install.Available(CrushTime.PatchId), "so the feature is available");
            Check.False(session.Install.Applied(CrushTime.PatchId), "and not yet applied");
        }

        public static void Test_ACfgCommentOrOtherSectionDoesNotMakeAClassPlayable()
        {
            string className;
            Check.False(CrushTime.IsHumanPlayable(System.Text.Encoding.ASCII.GetBytes(
                "[Unit]\r\n;HumanPlayable=YES\r\n[Other]\r\nHumanPlayable=YES\r\n"), out className),
                "only an uncommented key in [Unit] counts");
            Check.True(CrushTime.IsHumanPlayable(System.Text.Encoding.ASCII.GetBytes(
                "[Unit]\r\nClassName=SSTypeVIIC\r\nHumanPlayable = YES ; player boat\r\n"), out className),
                "whitespace and a trailing comment are fine");
            Check.Equal("SSTypeVIIC", className, "and the class name is read");
        }

        public static void Test_AnInstallWithNoSubmarineFolderHasNoCrushFeature()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            Check.Equal(0, session.Install.UBoats.Count, "no classes");
            Check.False(session.Install.Available(CrushTime.PatchId), "the feature is unavailable");
            WriteReport report = session.Apply(Crush(session, 5));
            Check.True(report.Ok, "asking for it is not an error");
            Check.Equal("Nothing to apply.", report.Message, "there is just nothing to write");
        }

        // -- writing -----------------------------------------------------------

        public static void Test_ApplyingWritesHitPointsOverCrushTimeToEveryPlayableClass()
        {
            Fixtures.Install fx = WithBoats();
            byte[] aiBefore = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat14"));
            byte[] viicBefore = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c"));
            PatchSession session = fx.Session();

            WriteReport report = session.Apply(Crush(session, 5));
            Check.True(report.Ok, report.Message);
            Check.Equal(3, report.SitesWritten.Count, "one write per playable class");
            Check.Contains("Crush time (3 U-boat files)", report.Message, "the confirmation counts files");

            Check.Equal(100f, Fixtures.CrashSpeedIn(ZonOf(fx, "NSS_Uboat7c")), "VIIC: 500 / 5");
            Check.Equal(80f, Fixtures.CrashSpeedIn(ZonOf(fx, "NSS_Uboat2A")), "IIA: 400 / 5");
            Check.Equal(120f, Fixtures.CrashSpeedIn(ZonOf(fx, "NSS_Uboat21")), "XXI: 600 / 5");
            Check.Bytes(aiBefore, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat14")), "an AI class is untouched");
            Check.False(File.Exists(ZonOf(fx, "NSS_Uboat14") + ".ape-orig"), "and gets no copy");

            // Only CrashSpeed's four bytes moved.
            byte[] viicAfter = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c"));
            Check.Equal(viicBefore.Length, viicAfter.Length, "same length");
            int differing = 0;
            for (int i = 0; i < viicBefore.Length; i++) if (viicBefore[i] != viicAfter[i]) differing++;
            Check.True(differing > 0 && differing <= 4, "at most four bytes differ");
            Check.Bytes(viicBefore, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig"),
                "the pristine copy is the file as it was");
        }

        public static void Test_AnAppliedInstallReadsBackItsCrushTime()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Crush(session, 37)).Ok, "applying 37 s");

            PatchSession after = fx.Session();
            Check.True(after.Install.Applied(CrushTime.PatchId), "reads as applied");
            Check.True(after.Current.CrushApplied, "the checkbox opens checked");
            Check.Equal(37, after.Current.CrushTime, "with the crush time APE wrote");
            Check.True(after.Plan(after.Current.Clone()).Empty, "and applying it again changes nothing");
        }

        public static void Test_AnUnappliedInstallOpensOnTheDefaultOfFiveSeconds()
        {
            PatchSession session = WithBoats().Session();
            Check.False(session.Current.CrushApplied, "unchecked");
            Check.Equal(5, session.Current.CrushTime, "default 5 s");
        }

        public static void Test_ChangingTheCrushTimeKeepsTheFirstPristineCopy()
        {
            Fixtures.Install fx = WithBoats();
            byte[] original = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c"));

            PatchSession first = fx.Session();
            Check.True(first.Apply(Crush(first, 5)).Ok, "first apply");
            PatchSession second = fx.Session();
            Check.True(second.Apply(Crush(second, 250)).Ok, "second apply, a new time");

            Check.Equal(2f, Fixtures.CrashSpeedIn(ZonOf(fx, "NSS_Uboat7c")), "VIIC: 500 / 250");
            Check.Bytes(original, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig"),
                "the copy still holds the file from before APE's first write");
        }

        public static void Test_UncheckingRestoresEveryZonByteForByteAndRemovesTheCopies()
        {
            Fixtures.Install fx = WithBoats();
            string[] folders = { "NSS_Uboat7c", "NSS_Uboat2A", "NSS_Uboat21" };
            byte[][] before = new byte[folders.Length][];
            for (int i = 0; i < folders.Length; i++) before[i] = File.ReadAllBytes(ZonOf(fx, folders[i]));

            PatchSession session = fx.Session();
            Check.True(session.Apply(Crush(session, 5)).Ok, "applying");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.CrushApplied = false;
            WriteReport report = applied.Apply(off);
            Check.True(report.Ok, report.Message);
            Check.Equal(3, report.SitesWritten.Count, "three files restored");

            for (int i = 0; i < folders.Length; i++)
            {
                Check.Bytes(before[i], File.ReadAllBytes(ZonOf(fx, folders[i])), folders[i] + " restored");
                Check.False(File.Exists(ZonOf(fx, folders[i]) + ".ape-orig"), folders[i] + " copy removed");
            }
            PatchSession after = fx.Session();
            Check.False(after.Install.Applied(CrushTime.PatchId), "and it reads as not applied");
        }

        // -- drift and unreadable files ----------------------------------------

        public static void Test_AZonReplacedByAnotherModIsShownAndNeverWritten()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Crush(session, 5)).Ok, "applying");

            // Another mod drops its own VIIC .zon over APE's.
            byte[] theirs = Fixtures.Zon(520f, 0.5f);
            File.WriteAllBytes(ZonOf(fx, "NSS_Uboat7c"), theirs);
            byte[] copy = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig");

            PatchSession drifted = fx.Session();
            UBoatZon viic = drifted.Install.UBoats[2];
            Check.Equal(ZonState.ChangedByOtherMod, viic.State, "read as changed by another mod");
            Check.True(drifted.Install.Applied(CrushTime.PatchId), "the others are still APE's");

            Review review = Review.Build(drifted.Install, drifted.Plan(Crush(drifted, 40)), Crush(drifted, 40));
            Check.Contains("NSS_Uboat7c.zon was changed by another mod", AllText(review), "and the review says so");

            Check.True(drifted.Apply(Crush(drifted, 40)).Ok, "a new time still applies");
            Check.Bytes(theirs, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c")), "but not to the other mod's file");
            Check.Equal(10f, Fixtures.CrashSpeedIn(ZonOf(fx, "NSS_Uboat2A")), "the others take it: 400 / 40");

            PatchSession again = fx.Session();
            PatchState off = again.Current.Clone();
            off.CrushApplied = false;
            Check.True(again.Apply(off).Ok, "unchecking");
            Check.Bytes(theirs, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c")), "the other mod's file is still left alone");
            Check.Bytes(copy, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig"), "and so is APE's copy of it");
        }

        public static void Test_AnotherModChangingOnlyCrashSpeedIsStillCaught()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Crush(session, 5)).Ok, "applying");

            // Same file, but a CrashSpeed no whole-second crush time produces.
            File.WriteAllBytes(ZonOf(fx, "NSS_Uboat2A"), Fixtures.Zon(400f, 0.77f));

            PatchSession after = fx.Session();
            Check.Equal(ZonState.ChangedByOtherMod, after.Install.UBoats[1].State, "not a value APE wrote");
        }

        public static void Test_AZonWithoutOneOfEachKeyIsNeverWritten()
        {
            Fixtures.Install fx = WithBoats();
            byte[] doubled = Fixtures.Zon(500f, 0.08f);
            byte[] extra = Fixtures.Zon(500f, 0.08f);
            byte[] bad = new byte[doubled.Length + extra.Length];
            doubled.CopyTo(bad, 0);
            extra.CopyTo(bad, doubled.Length);     // Hit_Points and CrashSpeed now occur twice
            File.WriteAllBytes(ZonOf(fx, "NSS_Uboat7c"), bad);

            PatchSession session = fx.Session();
            UBoatZon viic = session.Install.UBoats[2];
            Check.Equal(ZonState.Unreadable, viic.State, "unreadable");
            Check.Contains("occurs 2 times", viic.Problem, "and says why");

            Check.True(session.Apply(Crush(session, 5)).Ok, "the readable classes still apply");
            Check.Bytes(bad, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c")), "the unreadable one is untouched");
            Check.False(File.Exists(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig"), "and gets no copy");
        }

        public static void Test_AWrongRecordLengthOrAKeyOutsideTheBlockIsUnreadable()
        {
            string problem;
            byte[] zon = Fixtures.Zon(500f, 0.08f);
            int at = IndexOf(zon, "CrashSpeed\0");
            zon[at - 4] = 14;
            Check.True(Zon.Parse(zon, out problem) == null, "a record length of 14 is refused");
            Check.Contains("record length", problem, "and named");

            // Only one ColisionableObject: no data block to search.
            byte[] noBlock = Fixtures.Zon(500f, 0.08f);
            int second = IndexOf(noBlock, "ColisionableObject\0", IndexOf(noBlock, "ColisionableObject\0") + 1);
            noBlock[second] = (byte)'X';
            Check.True(Zon.Parse(noBlock, out problem) == null, "one ColisionableObject is refused");

            Check.True(Zon.Parse(Fixtures.Zon(500f, 0.08f), out problem) != null, "the well-formed file parses");
        }

        public static void Test_AZonChangedAfterOpeningRefusesTheWholeApply()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();
            byte[] exeBefore = fx.ExeBytes();

            File.WriteAllBytes(ZonOf(fx, "NSS_Uboat2A"), Fixtures.Zon(400f, 1f));
            PatchState want = Crush(session, 5);
            want.HullApplied = true;
            WriteReport report = session.Apply(want);

            Check.False(report.Ok, "refused");
            Check.Contains("changed after APE opened", report.Message, "and says why");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe untouched");
            Check.False(File.Exists(ZonOf(fx, "NSS_Uboat7c") + ".ape-orig"), "no .zon written");
        }

        // -- validation and failure --------------------------------------------

        public static void Test_ACrushTimeOutsideOneToThreeThousandSixHundredIsRefused()
        {
            Fixtures.Install fx = WithBoats();
            byte[] before = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c"));
            foreach (int bad in new int[] { 0, -5, 3601 })
            {
                PatchSession session = fx.Session();
                WriteReport report = session.Apply(Crush(session, bad));
                Check.False(report.Ok, Fmt.Int(bad) + " s is refused");
                Check.Contains("crush time", report.Message, "and named");
            }
            Check.Bytes(before, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat7c")), "nothing written");

            PatchSession edge = fx.Session();
            Check.True(edge.Apply(Crush(edge, 3600)).Ok, "3600 s is allowed");
        }

        public static void Test_AZonWriteFailurePutsBackEveryFileThisAttemptWrote()
        {
            Fixtures.Install fx = WithBoats();
            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();
            byte[] firstZon = File.ReadAllBytes(ZonOf(fx, "NSS_Uboat21"));

            PatchSession session = fx.Session();
            Action<string, byte[]> real = session.WriteZonBytes;
            string failOn = ZonOf(fx, "NSS_Uboat2A");
            session.WriteZonBytes = delegate(string path, byte[] bytes)
            {
                if (path == failOn) throw new IOException("disk full");
                real(path, bytes);
            };

            PatchState want = Crush(session, 5);
            want.HullApplied = true;       // so sh3.exe is written first and must come back
            WriteReport report = session.Apply(want);

            Check.False(report.Ok, "the commit fails");
            Check.Contains("disk full", report.Message, "naming the failure");
            Check.Equal(0, report.SitesWritten.Count, "and claims nothing written");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe put back");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini put back");
            Check.Bytes(firstZon, File.ReadAllBytes(ZonOf(fx, "NSS_Uboat21")), "the .zon already written put back");
            Check.False(File.Exists(ZonOf(fx, "NSS_Uboat21") + ".ape-orig"), "and its new copy removed");
            Check.False(File.Exists(ZonOf(fx, "NSS_Uboat2A") + ".ape-orig"), "the failing one's copy removed");
        }

        // -- arithmetic --------------------------------------------------------

        public static void Test_ACrushSpeedAPEWroteRoundTripsToItsCrushTime()
        {
            foreach (float hp in new float[] { 150f, 400f, 500f, 550f, 600f, 10000f })
                for (int t = PatchState.CrushTimeMin; t <= PatchState.CrushTimeMax; t += 7)
                    Check.Equal(t, Zon.CrushTimeOf(hp, Zon.CrashSpeedFor(hp, t)),
                        "hull " + Fmt.F1(hp) + ", " + Fmt.Int(t) + " s");
            Check.Equal(0, Zon.CrushTimeOf(500f, 0f), "zero crush speed is not APE's");
            Check.Equal(0, Zon.CrushTimeOf(500f, 0.08f), "6250 s is beyond the range");
        }

        // -- review ------------------------------------------------------------

        public static void Test_TheReviewNamesEveryZonAndWhenItTakesEffect()
        {
            Fixtures.Install fx = WithBoats();
            PatchSession session = fx.Session();
            PatchState want = Crush(session, 5);
            Review r = Review.Build(session.Install, session.Plan(want), want);

            Check.Equal(1, r.FeatureCount, "one feature");
            string text = AllText(r);
            Check.Contains("Crush time", text, "the group is named");
            Check.Contains("about 5 seconds", text, "with the time");
            Check.Contains("data/Submarine/NSS_Uboat7c/NSS_Uboat7c.zon", text, "VIIC named");
            Check.Contains("data/Submarine/NSS_Uboat2A/NSS_Uboat2A.zon", text, "IIA named");
            Check.Contains("data/Submarine/NSS_Uboat21/NSS_Uboat21.zon", text, "XXI named");
            Check.DoesNotContain("NSS_Uboat14", text, "the AI class is not");
            Check.Contains("100.00 hull per second", text, "the VIIC's crush speed");
            Check.Contains("AI U-boats of these classes change too", text, "shared files are called out");
            Check.Contains("next mission", text, "and when it takes effect");

            Check.True(session.Apply(want).Ok, "applying");
            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.CrushApplied = false;
            string back = AllText(Review.Build(applied.Install, applied.Plan(off), off));
            Check.Contains("own crush speed comes back", back, "the revert says what returns");
            Check.Contains("Restores data/Submarine/NSS_Uboat7c/NSS_Uboat7c.zon", back, "and names the files");
        }

        // -- helpers -----------------------------------------------------------

        static string AllText(Review r)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (ReviewGroup g in r.Groups)
            {
                sb.AppendLine(g.Title);
                foreach (ReviewLine l in g.Lines) sb.AppendLine(l.Text);
            }
            if (r.NothingText != null) sb.AppendLine(r.NothingText);
            return sb.ToString();
        }

        static int IndexOf(byte[] hay, string needle) { return IndexOf(hay, needle, 0); }

        static int IndexOf(byte[] hay, string needle, int from)
        {
            byte[] n = System.Text.Encoding.ASCII.GetBytes(needle);
            for (int i = from; i + n.Length <= hay.Length; i++)
            {
                int j = 0;
                while (j < n.Length && hay[i + j] == n[j]) j++;
                if (j == n.Length) return i;
            }
            return -1;
        }
    }
}
