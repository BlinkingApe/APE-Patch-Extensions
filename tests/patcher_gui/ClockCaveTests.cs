// The depth stress clock fix (depth-pressure-stress ticket 36): GWX-KC 2.5's sh3.exe
// lacks h.sie's clock-mirror cave, so ape.act's clock anchor never resolves there and
// depth stress does nothing. APE can write OAE's exact cave bytes into it, keeping
// sh3.exe.ape-orig, and put the two sites back on revert.
//
// Like the other session tests, these go through PatchSession only.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class ClockCaveTests
    {
        static PatchState Cave(PatchSession session, bool on)
        {
            PatchState s = session.Current.Clone();
            s.ClockCaveApplied = on;
            return s;
        }

        // -- discovery ---------------------------------------------------------

        public static void Test_AKcShapedExeOffersTheCaveUnapplied()
        {
            PatchSession session = Fixtures.KcShaped().Session();
            Check.Equal(ClockCaveState.Missing, session.Install.ClockCave.State, "no cave, stock store");
            Check.True(session.Install.Available(ClockCave.PatchId), "the cave is offered");
            Check.False(session.Install.Applied(ClockCave.PatchId), "and not yet applied");
            Check.False(session.Current.ClockCaveApplied, "so the box opens unchecked");
        }

        // -- writing -----------------------------------------------------------

        public static void Test_ApplyingWritesOaesCaveAndKeepsACopyOfSh3Exe()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();

            WriteReport report = session.Apply(Cave(session, true));
            Check.True(report.Ok, report.Message);
            Check.Contains(ClockCave.PatchName, report.Message, "the confirmation names the fix");
            byte[] after = fx.ExeBytes();
            Check.Bytes(Fixtures.CaveJump, Fixtures.Slice(after, Fixtures.ClockSiteOffset, 6), "the jump");
            Check.Bytes(Fixtures.CaveBody, Fixtures.Slice(after, Fixtures.ClockCaveOffset, 16), "the cave");
            int differing = 0;
            for (int i = 0; i < after.Length; i++) if (after[i] != before[i]) differing++;
            Check.True(differing <= 22, "nothing else in sh3.exe changes");
            Check.Bytes(before, File.ReadAllBytes(fx.Sh3Exe + ".ape-orig"), "the copy is sh3.exe as it was");
        }

        public static void Test_AnAppliedCaveReadsBackWithTheClockResolvingToTheMirror()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Cave(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.Applied, after.Install.ClockCave.State, "reads as APE's");
            Check.Equal(1, after.Install.ClockCave.ClockMatches, "the clock signature matches once");
            Check.Equal(0x0063FF80u, after.Install.ClockCave.ClockTarget, "and names the mirror");
            Check.True(after.Current.ClockCaveApplied, "so the box opens checked");
            Check.True(after.Plan(after.Current).Empty, "opening and applying changes nothing");
        }

        public static void Test_UncheckingRestoresSh3ExeByteForByteAndRemovesTheCopy()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Cave(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            WriteReport report = after.Apply(Cave(after, false));
            Check.True(report.Ok, report.Message);
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe is back as it was");
            Check.False(File.Exists(fx.Sh3Exe + ".ape-orig"), "and APE's copy is gone");
            Check.Equal(ClockCaveState.Missing, fx.Session().Install.ClockCave.State, "as it was before APE");
        }

        public static void Test_RevertKeepsOtherFeaturesWrittenToSh3ExeSince()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Cave(session, true)).Ok, "the cave");
            PatchSession second = fx.Session();
            PatchState hull = second.Current.Clone();
            hull.HullApplied = true;
            Check.True(second.Apply(hull).Ok, "then hull repair");

            PatchSession third = fx.Session();
            Check.True(third.Apply(Cave(third, false)).Ok, "taking the cave out");
            PatchSession after = fx.Session();
            Check.True(after.Install.Applied(Manifest.HullPatchId), "hull repair stays");
            Check.Equal(ClockCaveState.Missing, after.Install.ClockCave.State, "the cave is gone");
        }

        public static void Test_AnOaeShapedExeIsNeverOfferedOrWritten()
        {
            Fixtures.Install fx = Fixtures.OaeShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            Check.Equal(ClockCaveState.NotNeeded, session.Install.ClockCave.State, "h.sie's own cave");
            Check.False(session.Install.Available(ClockCave.PatchId), "not offered");
            Check.False(session.Current.ClockCaveApplied, "and not APE's");

            WriteReport on = session.Apply(Cave(session, true));
            Check.True(on.Ok, on.Message);
            Check.Equal("Nothing to apply.", on.Message, "asking for it writes nothing");
            WriteReport off = fx.Session().Apply(Cave(fx.Session(), false));
            Check.Equal("Nothing to apply.", off.Message, "nor does asking for it off");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe untouched");
            Check.False(File.Exists(fx.Sh3Exe + ".ape-orig"), "no copy made");
        }

        static void RefusedWith(int offset, byte value, string what)
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] exe = fx.ExeBytes();
            exe[offset] = value;
            File.WriteAllBytes(fx.Sh3Exe, exe);
            PatchSession session = fx.Session();
            Check.Equal(ClockCaveState.Unrecognised, session.Install.ClockCave.State, what);
            Check.False(session.Install.Available(ClockCave.PatchId), what + ": not offered");
            Check.Equal("Nothing to apply.", session.Apply(Cave(session, true)).Message, what + ": nothing written");
            Check.Bytes(exe, fx.ExeBytes(), what + ": sh3.exe untouched");
            Check.False(File.Exists(fx.Sh3Exe + ".ape-orig"), what + ": no copy made");
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
            Check.True(session.Apply(Cave(session, true)).Ok, "applying");
            File.WriteAllBytes(fx.Sh3Exe, stock);

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.Missing, after.Install.ClockCave.State, "missing again");
            Check.False(after.Current.ClockCaveApplied, "so the box opens unchecked");
            Check.True(after.Apply(Cave(after, true)).Ok, "and it can be applied again");
            Check.Equal(ClockCaveState.Applied, fx.Session().Install.ClockCave.State, "applied");
            Check.Bytes(stock, File.ReadAllBytes(fx.Sh3Exe + ".ape-orig"), "the copy is still the first one");
        }

        public static void Test_AnExeChangedAfterTheCaveIsLeftAlone()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            Check.True(session.Apply(Cave(session, true)).Ok, "applying");
            byte[] exe = fx.ExeBytes();
            exe[Fixtures.ClockSiteOffset] = 0xCC;
            File.WriteAllBytes(fx.Sh3Exe, exe);

            PatchSession after = fx.Session();
            Check.Equal(ClockCaveState.ChangedByOtherMod, after.Install.ClockCave.State, "someone else's now");
            Check.False(after.Install.Available(ClockCave.PatchId), "not offered");
            Check.Equal("Nothing to apply.", after.Apply(Cave(after, false)).Message, "unchecking writes nothing");
            Check.Bytes(exe, fx.ExeBytes(), "sh3.exe untouched");
            Check.True(File.Exists(fx.Sh3Exe + ".ape-orig"), "nor is APE's copy deleted");
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

            PatchState want = Cave(session, true);
            want.MotorZoneFixApplied = true;
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "the apply fails");
            Check.Contains("disk full", report.Message, "with the cause");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe is put back");
            Check.False(File.Exists(fx.Sh3Exe + ".ape-orig"), "and the copy made this attempt is removed");
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

            PatchState want = Cave(session, true);
            want.DieselApplied = true;   // diesel damage writes hsie.ini
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "the apply fails");
            Check.Contains("inconsistent", report.Message, "and says the install is inconsistent");
            Check.True(File.Exists(fx.Sh3Exe + ".ape-orig"), "the copy stays beside the patched exe");
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

            WriteReport report = session.Apply(Cave(session, true));
            Check.False(report.Ok, "the apply fails");
            Check.Contains("access denied", report.Message, "with the cause");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe unchanged");
            Check.False(File.Exists(fx.Sh3Exe + ".ape-orig"), "no copy left behind");
        }

        public static void Test_ACopyAppearingAfterOpeningRefusesTheWholeApply()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            byte[] before = fx.ExeBytes();
            PatchSession session = fx.Session();
            File.WriteAllBytes(fx.Sh3Exe + ".ape-orig", before);

            WriteReport report = session.Apply(Cave(session, true));
            Check.False(report.Ok, "refused");
            Check.Contains("changed after APE opened", report.Message, "says why");
            Check.Bytes(before, fx.ExeBytes(), "sh3.exe untouched");
        }

        // -- review ------------------------------------------------------------

        public static void Test_TheReviewSaysWhatTheFixDoesForDepthStress()
        {
            Fixtures.Install fx = Fixtures.KcShaped();
            PatchSession session = fx.Session();
            PatchState on = Cave(session, true);
            Review r = Review.Build(session.Install, session.Plan(on), on);
            Check.Equal(1, r.FeatureCount, "one feature");
            Check.Equal(ClockCave.PatchName, r.Groups[0].Title, "titled by the fix");
            string text = string.Join("\n", r.Groups[0].Lines.ConvertAll(l => l.Text).ToArray());
            Check.Contains("depth stress", text, "what it is for");
            Check.Contains("next time you start SH3", text, "when it takes effect");
            Check.DoesNotContain("0x", text, "no addresses");
            Check.DoesNotContain("sh3.exe", text, "no file names");

            Check.True(session.Apply(on).Ok, "applying");
            PatchSession after = fx.Session();
            PatchState off = Cave(after, false);
            Review back = Review.Build(after.Install, after.Plan(off), off);
            Check.Equal(ClockCave.PatchName, back.Groups[0].Title, "unchecking shows too");
        }
    }
}
