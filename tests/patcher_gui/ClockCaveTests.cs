// h.sie's clock cave, added with depth stress (depth-pressure-stress ticket 36).
// GWX-KC 2.5's sh3.exe lacks the cave that mirrors the mission clock, so ape.act's
// clock anchor never resolves there and depth stress does nothing. Checking depth
// stress on such an exe quietly writes h.sie's cave bytes into it too, keeping
// sh3.exe.ape-orig; unchecking depth stress puts the two sites back.
//
// Like the other session tests, these go through PatchSession only.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class ClockCaveTests
    {
        static PatchState Depth(PatchSession session, bool on)
        {
            PatchState s = session.Current.Clone();
            s.DepthApplied = on;
            return s;
        }

        static string Copy(Fixtures.Install fx) { return fx.Sh3Exe + ".ape-orig"; }

        // -- discovery ---------------------------------------------------------

        public static void Test_AKcShapedExeNeedsTheCave()
        {
            PatchSession session = Fixtures.KcShaped().Session();
            Check.Equal(ClockCaveState.Missing, session.Install.ClockCave.State, "no cave, stock store");
            Check.True(session.Install.Available(ClockCave.PatchId), "APE can add it");
            Check.False(session.Install.Applied(ClockCave.PatchId), "and hasn't yet");
        }

        // -- writing -----------------------------------------------------------

        public static void Test_CheckingDepthStressAddsTheCaveAndKeepsACopyOfSh3Exe()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();

            WriteReport report = session.Apply(Depth(session, true));
            Check.True(report.Ok, report.Message);
            byte[] after = fx.ExeBytes();
            Check.Bytes(Fixtures.CaveJump, Fixtures.Slice(after, Fixtures.ClockSiteOffset, 6), "the jump");
            Check.Bytes(Fixtures.CaveBody, Fixtures.Slice(after, Fixtures.ClockCaveOffset, 16), "the cave");
            int differing = 0;
            for (int i = 0; i < after.Length; i++) if (after[i] != before[i]) differing++;
            Check.True(differing <= 22, "nothing else in sh3.exe changes");
            Check.Bytes(before, File.ReadAllBytes(Copy(fx)), "the copy is sh3.exe as it was");
            Check.True(fx.Session().Install.Applied(DepthStress.PatchId), "and ape.act is deployed");
        }

        public static void Test_AnAppliedCaveReadsBackWithTheClockResolvingToTheMirror()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Depth(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.Applied, after.Install.ClockCave.State, "reads as APE's");
            Check.Equal(1, after.Install.ClockCave.ClockMatches, "the clock signature matches once");
            Check.Equal(0x0063FF80u, after.Install.ClockCave.ClockTarget, "and names the mirror");
            Check.True(after.Current.DepthApplied, "depth stress opens checked");
            Check.True(after.Plan(after.Current).Empty, "opening and applying changes nothing");
        }

        public static void Test_DepthStressDeployedWithoutTheCaveGetsItOnTheNextApply()
        {
            // An install where an earlier APE deployed ape.act before it knew to add
            // the cave: opening and applying, changing nothing else, fixes it.
            Fixtures.Install fx = Fixtures.KcShaped();
            File.WriteAllBytes(Path.Combine(fx.Dir, DepthStress.FileName), fx.ApeActTemplate());
            PatchSession session = fx.Session();
            Check.True(session.Current.DepthApplied, "depth stress reads as on");

            WritePlan plan = session.Plan(session.Current);
            Check.True(plan.Touches(ClockCave.PatchId), "the cave is planned");
            Check.Equal(1, plan.FeatureCount, "as part of depth stress, one feature");
            Check.True(session.Apply(session.Current).Ok, "applying");
            Check.Equal(ClockCaveState.Applied, fx.Session().Install.ClockCave.State, "the cave is in");
        }

        public static void Test_UncheckingDepthStressRestoresSh3ExeByteForByte()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Depth(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            WriteReport report = after.Apply(Depth(after, false));
            Check.True(report.Ok, report.Message);
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe is back as it was");
            Check.False(File.Exists(Copy(fx)), "and APE's copy is gone");
            Check.False(fx.Session().Install.Applied(DepthStress.PatchId), "with ape.act");
            Check.Equal(ClockCaveState.Missing, fx.Session().Install.ClockCave.State, "as it was before APE");
        }

        public static void Test_RevertKeepsOtherFeaturesWrittenToSh3ExeSince()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Depth(session, true)).Ok, "depth stress");
            PatchSession second = fx.Session();
            PatchState hull = second.Current.Clone();
            hull.HullApplied = true;
            Check.True(second.Apply(hull).Ok, "then hull repair");

            PatchSession third = fx.Session();
            Check.True(third.Apply(Depth(third, false)).Ok, "taking depth stress out");
            PatchSession after = fx.Session();
            Check.True(after.Install.Applied(Manifest.HullPatchId), "hull repair stays");
            Check.Equal(ClockCaveState.Missing, after.Install.ClockCave.State, "the cave is gone");
        }

        public static void Test_AnExeWithHsiesOwnCaveIsNeverWritten()
        {
            Fixtures.Install fx = Fixtures.OaeShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Check.Equal(ClockCaveState.NotNeeded, session.Install.ClockCave.State, "h.sie's own cave");
            Check.False(session.Install.Available(ClockCave.PatchId), "nothing to add");

            WriteReport on = session.Apply(Depth(session, true));
            Check.True(on.Ok, on.Message);
            Check.True(fx.Session().Install.Applied(DepthStress.PatchId), "depth stress deploys");
            PatchSession again = fx.Session();
            Check.True(again.Apply(Depth(again, false)).Ok, "and comes out");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe untouched either way");
            Check.False(File.Exists(Copy(fx)), "no copy made");
        }

        static void RefusedWith(int offset, byte value, string what)
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] exe = fx.ExeBytes();
            exe[offset] = value;
            File.WriteAllBytes(fx.Sh3Exe, exe);
            PatchSession session = fx.Session();
            Check.Equal(ClockCaveState.Unrecognised, session.Install.ClockCave.State, what);
            Check.False(session.Install.Available(ClockCave.PatchId), what + ": APE won't add it");
            Check.True(session.Apply(Depth(session, true)).Ok, what + ": depth stress still applies");
            Check.Bytes(exe, fx.ExeBytes(), what + ": sh3.exe untouched");
            Check.False(File.Exists(Copy(fx)), what + ": no copy made");
        }

        public static void Test_UnexpectedBytesAtEitherSiteAreRefused()
        {
            RefusedWith(Fixtures.ClockSiteOffset + 2, 0x51, "another store at the clock site");
            RefusedWith(Fixtures.ClockCaveOffset + 15, 0xCC, "something in the slack");
        }

        public static void Test_AToolPuttingStockCodeBackReadsAsMissingAgain()
        {
            // h.sie's options tool may rewrite sh3.exe: if it puts the stock store
            // back, APE's copy is still there, and the cave is simply missing.
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] stock = fx.ExeBytes();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Depth(session, true)).Ok, "applying");
            File.WriteAllBytes(fx.Sh3Exe, stock);

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.Missing, after.Install.ClockCave.State, "missing again");
            Check.False(after.Plan(after.Current).Empty, "so applying with depth stress on adds it back");
            Check.True(after.Apply(after.Current).Ok, "applying");
            Check.Equal(ClockCaveState.Applied, fx.Session().Install.ClockCave.State, "applied");
            Check.Bytes(stock, File.ReadAllBytes(Copy(fx)), "the copy is still the first one");
        }

        public static void Test_AnExeChangedAfterTheCaveIsLeftAlone()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Depth(session, true)).Ok, "applying");
            byte[] exe = fx.ExeBytes();
            exe[Fixtures.ClockSiteOffset] = 0xCC;
            File.WriteAllBytes(fx.Sh3Exe, exe);

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.ChangedByOtherMod, after.Install.ClockCave.State, "someone else's now");
            Check.True(after.Apply(Depth(after, false)).Ok, "unchecking depth stress");
            Check.Bytes(exe, fx.ExeBytes(), "doesn't write sh3.exe");
            Check.True(File.Exists(Copy(fx)), "nor delete APE's copy");
        }

        public static void Test_AWriteFailureLeavesNoCopyAndNoPatchedExe()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            string zones = Fixtures.AddZonesCfg(fx, "24");
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Action<string, byte[]> real = session.WriteZonBytes;
            session.WriteZonBytes = delegate(string path, byte[] bytes)
            {
                if (string.Equals(path, zones, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("disk full");
                real(path, bytes);
            };

            PatchState want = Depth(session, true);
            want.MotorZoneFixApplied = true;
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "the apply fails");
            Check.Contains("disk full", report.Message, "with the cause");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe is put back");
            Check.False(File.Exists(Copy(fx)), "and the copy made this attempt is removed");
        }

        public static void Test_AnApeActWriteFailurePutsSh3ExeBack()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            session.WriteApeActBytes = delegate(string path, byte[] bytes) { throw new IOException("ape.act locked"); };

            WriteReport report = session.Apply(Depth(session, true));
            Check.False(report.Ok, "the apply fails");
            Check.Contains("ape.act locked", report.Message, "with the cause");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe is put back");
            Check.False(File.Exists(Copy(fx)), "and the copy is removed");
        }

        public static void Test_ACopyStaysWhenSh3ExeCannotBePutBack()
        {
            // hsie.ini fails, and so does putting sh3.exe back: sh3.exe keeps APE's
            // cave, so its copy must stay too, or the cave would read as h.sie's own
            // and could never be taken out.
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            session.SaveIni = delegate(IniFile f, string p) { throw new IOException("ini locked"); };
            int exeWrites = 0;
            Action<string, byte[]> real = session.WriteExeBytes;
            session.WriteExeBytes = delegate(string path, byte[] bytes)
            {
                if (string.Equals(path, fx.Sh3Exe, StringComparison.OrdinalIgnoreCase) && ++exeWrites > 1)
                    throw new IOException("exe locked");
                real(path, bytes);
            };

            PatchState want = Depth(session, true);
            want.DieselApplied = true;   // diesel damage writes hsie.ini
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "the apply fails");
            Check.Contains("inconsistent", report.Message, "and says the install is inconsistent");
            Check.True(File.Exists(Copy(fx)), "the copy stays beside the patched exe");
            Check.Equal(ClockCaveState.Applied, fx.Session().Install.ClockCave.State, "so the cave still reads as APE's");
        }

        public static void Test_AnExeWriteFailureRemovesTheCopy()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Action<string, byte[]> real = session.WriteExeBytes;
            session.WriteExeBytes = delegate(string path, byte[] bytes)
            {
                if (string.Equals(path, fx.Sh3Exe, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("access denied");
                real(path, bytes);
            };

            WriteReport report = session.Apply(Depth(session, true));
            Check.False(report.Ok, "the apply fails");
            Check.Contains("access denied", report.Message, "with the cause");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe unchanged");
            Check.False(File.Exists(Copy(fx)), "no copy left behind");
        }

        public static void Test_ACopyAppearingAfterOpeningRefusesTheWholeApply()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            File.WriteAllBytes(Copy(fx), before);

            WriteReport report = session.Apply(Depth(session, true));
            Check.False(report.Ok, "refused");
            Check.Contains("changed after APE opened", report.Message, "says why");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe untouched");
        }

        // -- review ------------------------------------------------------------

        public static void Test_TheCaveIsQuietlyPartOfDepthStressInTheReview()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            PatchState on = Depth(session, true);
            Review r = Review.Build(session.Install, session.Plan(on), on);
            Check.Equal(1, r.FeatureCount, "one feature");
            Check.Equal(DepthStress.PatchName, r.Groups[0].Title, "depth stress, no group of its own");

            Check.True(session.Apply(on).Ok, "applying");
            PatchSession after = fx.Session();
            PatchState off = Depth(after, false);
            Review back = Review.Build(after.Install, after.Plan(off), off);
            Check.Equal(1, back.FeatureCount, "unchecking: still one feature");
            Check.Equal(DepthStress.PatchName, back.Groups[0].Title, "depth stress");
        }
    }
}
