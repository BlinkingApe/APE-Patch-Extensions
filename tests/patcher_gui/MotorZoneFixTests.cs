// The starboard motor zone fix (depth-pressure-stress ticket 33): OAE's GWX damage
// model parents [ElectricEngine1], the starboard electric motor, to the bow quarters
// (Father=24) instead of the electric engine room (Father=30, vanilla). APE can set
// that one value to 30, keeping a pristine Zones.cfg.ape-orig for the revert.
//
// Like the other session tests, these go through PatchSession only.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class MotorZoneFixTests
    {
        static Fixtures.Install Faulty(out string zones)
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            zones = Fixtures.AddZonesCfg(fx, "24");
            return fx;
        }

        static PatchState Fix(PatchSession session, bool on)
        {
            PatchState s = session.Current.Clone();
            s.MotorZoneFixApplied = on;
            return s;
        }

        static string Text(string path) { return Fixtures.Latin1(File.ReadAllBytes(path)); }

        // -- discovery ---------------------------------------------------------

        public static void Test_AFaultyInstallOffersTheFixUnapplied()
        {
            string zones;
            PatchSession session = Faulty(out zones).Session();
            Check.Equal(ZonesState.Faulty, session.Install.Zones.State, "Father=24 is the fault");
            Check.True(session.Install.Available(MotorZoneFix.PatchId), "the fix is offered");
            Check.False(session.Install.Applied(MotorZoneFix.PatchId), "and not yet applied");
            Check.False(session.Current.MotorZoneFixApplied, "so the box opens unchecked");
        }

        public static void Test_AnInstallAlreadyCorrectNeedsNothing()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string zones = Fixtures.AddZonesCfg(fx, "30");
            byte[] before = File.ReadAllBytes(zones);
            PatchSession session = fx.Session();

            Check.Equal(ZonesState.NotNeeded, session.Install.Zones.State, "vanilla's Father=30");
            Check.False(session.Install.Available(MotorZoneFix.PatchId), "nothing to offer");
            WriteReport report = session.Apply(Fix(session, true));
            Check.True(report.Ok, "asking for it is not an error");
            Check.Equal("Nothing to apply.", report.Message, "there is just nothing to write");
            Check.Bytes(before, File.ReadAllBytes(zones), "the file is untouched");
        }

        public static void Test_AnInstallWithNoZonesCfgHasNoFix()
        {
            PatchSession session = Fixtures.Stock(true).Session();
            Check.Equal(ZonesState.Absent, session.Install.Zones.State, "no data/Zones.cfg");
            Check.False(session.Install.Available(MotorZoneFix.PatchId), "nothing to offer");
        }

        public static void Test_OnlyAnUncommentedFatherInsideElectricEngine1Counts()
        {
            string value;
            byte[] file = System.Text.Encoding.ASCII.GetBytes(
                "[ElectricEngineRoom]\r\nFather=24\r\n[ElectricEngine1]\r\n;Father=24\r\n"
                + "  Father = 24 ; GWX\r\n[ElectricEngine2]\r\nFather=24\r\n");
            int at = MotorZoneFix.FatherValueAt(file, out value);
            Check.Equal("24", value, "the value, without spaces or the comment");
            Check.Equal("24 ; GWX", Fixtures.Latin1(file).Substring(at, 8), "found at its own line");
            Check.True(at > Fixtures.Latin1(file).IndexOf("[ElectricEngine1]", StringComparison.Ordinal),
                "inside [ElectricEngine1], not the section before");

            byte[] none = System.Text.Encoding.ASCII.GetBytes("[ElectricEngine1]\r\nHitPoints=180\r\n");
            Check.Equal(-1, MotorZoneFix.FatherValueAt(none, out value), "no Father line: not found");
        }

        public static void Test_AZonesCfgWithNoFatherLineIsLeftAlone()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string zones = Path.Combine(Path.Combine(fx.Dir, "data"), "Zones.cfg");
            File.WriteAllText(zones, "[ElectricEngine1]\r\nHitPoints=180\r\n");
            PatchSession session = fx.Session();
            Check.Equal(ZonesState.Unreadable, session.Install.Zones.State, "nothing APE can safely change");
            Check.False(session.Install.Available(MotorZoneFix.PatchId), "not offered");
        }

        // -- writing -----------------------------------------------------------

        public static void Test_ApplyingChangesOnlyThatValueAndKeepsAPristineCopy()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            byte[] before = File.ReadAllBytes(zones);
            PatchSession session = fx.Session();

            WriteReport report = session.Apply(Fix(session, true));
            Check.True(report.Ok, report.Message);
            Check.Contains(MotorZoneFix.PatchName, report.Message, "the confirmation names the fix");
            Check.Equal(Fixtures.ZonesCfgText("30"), Text(zones), "Father=24 became Father=30, nothing else");
            Check.Contains(";Father=24", Text(zones), "a comment elsewhere is left as it was");
            Check.Bytes(before, File.ReadAllBytes(zones + ".ape-orig"), "the copy is the file as it was");
        }

        public static void Test_AnAppliedInstallReadsBackAndReapplyingWritesNothing()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            PatchSession session = fx.Session();
            Check.True(session.Apply(Fix(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            Check.Equal(ZonesState.Applied, after.Install.Zones.State, "reads as APE's");
            Check.True(after.Install.Applied(MotorZoneFix.PatchId), "applied");
            Check.True(after.Current.MotorZoneFixApplied, "so the box opens checked");
            Check.True(after.Plan(after.Current).Empty, "opening and applying changes nothing");
        }

        public static void Test_UncheckingRestoresTheFileByteForByteAndRemovesTheCopy()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            byte[] before = File.ReadAllBytes(zones);
            PatchSession session = fx.Session();
            Check.True(session.Apply(Fix(session, true)).Ok, "applying");

            PatchSession after = fx.Session();
            WriteReport report = after.Apply(Fix(after, false));
            Check.True(report.Ok, report.Message);
            Check.Bytes(before, File.ReadAllBytes(zones), "the install's own Zones.cfg is back");
            Check.False(File.Exists(zones + ".ape-orig"), "and APE's copy is gone");
            Check.Equal(ZonesState.Faulty, fx.Session().Install.Zones.State, "as it was before APE");
        }

        public static void Test_AZonesCfgChangedByAnotherModAfterAPEIsNeverWritten()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            PatchSession session = fx.Session();
            Check.True(session.Apply(Fix(session, true)).Ok, "applying");

            File.AppendAllText(zones, "[SomeOtherMod]\r\nFather=1\r\n");
            byte[] modded = File.ReadAllBytes(zones);
            PatchSession after = fx.Session();
            Check.Equal(ZonesState.ChangedByOtherMod, after.Install.Zones.State, "another mod's file now");
            Check.False(after.Install.Available(MotorZoneFix.PatchId), "not offered");

            WriteReport report = after.Apply(Fix(after, false));
            Check.True(report.Ok, report.Message);
            Check.Bytes(modded, File.ReadAllBytes(zones), "unchecking doesn't overwrite the other mod");
            Check.True(File.Exists(zones + ".ape-orig"), "nor delete APE's copy");
        }

        public static void Test_AZonesCfgChangedAfterOpeningRefusesTheWholeApply()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            PatchSession session = fx.Session();
            File.AppendAllText(zones, "; touched\r\n");
            byte[] touched = File.ReadAllBytes(zones);

            PatchState want = Fix(session, true);
            want.HullApplied = true;
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "refused");
            Check.Contains("changed after APE opened", report.Message, "says why");
            Check.Bytes(touched, File.ReadAllBytes(zones), "Zones.cfg untouched");
            Check.False(File.Exists(zones + ".ape-orig"), "no copy made");
            Check.False(fx.Session().Install.Applied(Manifest.HullPatchId), "and nothing else written either");
        }

        public static void Test_AWriteFailurePutsBackEveryFileThisAttemptWrote()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            byte[] zonesBefore = File.ReadAllBytes(zones);
            byte[] exeBefore = fx.ExeBytes();
            PatchSession session = fx.Session();
            Action<string, byte[]> real = session.WriteZonBytes;
            session.WriteZonBytes = delegate(string path, byte[] bytes)
            {
                if (string.Equals(path, zones, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("disk full");
                real(path, bytes);
            };

            PatchState want = Fix(session, true);
            want.HullApplied = true;
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "the apply fails");
            Check.Contains("disk full", report.Message, "with the cause");
            Check.Bytes(zonesBefore, File.ReadAllBytes(zones), "Zones.cfg as it was");
            Check.False(File.Exists(zones + ".ape-orig"), "the copy made this attempt is removed");
            Check.Bytes(exeBefore, fx.ExeBytes(), "and sh3.exe is put back too");
        }

        // -- review ------------------------------------------------------------

        public static void Test_TheReviewSaysWhereTheMotorGoesInGameTerms()
        {
            string zones;
            Fixtures.Install fx = Faulty(out zones);
            PatchSession session = fx.Session();
            PatchState on = Fix(session, true);
            Review r = Review.Build(session.Install, session.Plan(on), on);
            Check.Equal(1, r.FeatureCount, "one feature");
            Check.Equal(MotorZoneFix.PatchName, r.Groups[0].Title, "titled by the fix");
            string text = string.Join("\n", r.Groups[0].Lines.ConvertAll(l => l.Text).ToArray());
            Check.Contains("electric engine room", text, "where the motor goes");
            Check.Contains("next time you start SH3", text, "when it takes effect");
            Check.DoesNotContain("Father", text, "no file keys");

            Check.True(session.Apply(on).Ok, "applying");
            PatchSession after = fx.Session();
            PatchState off = Fix(after, false);
            Review back = Review.Build(after.Install, after.Plan(off), off);
            string backText = string.Join("\n", back.Groups[0].Lines.ConvertAll(l => l.Text).ToArray());
            Check.Contains("bow quarters", backText, "unchecking says where it goes back to");
        }
    }
}
