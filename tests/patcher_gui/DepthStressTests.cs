// Depth stress's ape.act store (depth-pressure-stress ticket 22, ADR-0006): the
// patcher copies its embedded ape.act template into the game root, checks the
// constants struct's marker at the manifest's offset, and writes the player's
// numbers there in runtime units. Unchecking deletes ape.act.
//
// Like the other session tests, these go through PatchSession only: open a
// prepared game directory, apply, and assert what the files became.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class DepthStressTests
    {
        static string ApeActOf(Fixtures.Install fx)
        {
            return Path.Combine(fx.Dir, "ape.act");
        }

        static ApeActStruct Struct(Fixtures.Install fx)
        {
            return fx.Manifest.DepthStressStruct;
        }

        static float FieldIn(byte[] image, Fixtures.Install fx, string field)
        {
            ApeActStruct s = Struct(fx);
            return BitConverter.ToSingle(image, s.Offset + s.FieldAt(field));
        }

        static PatchState Enabled(PatchSession session)
        {
            PatchState s = session.Current.Clone();
            s.DepthApplied = true;
            return s;
        }

        // -- deploying ---------------------------------------------------------

        public static void Test_EnablingDeploysApeActWithTheEnteredValuesInRuntimeUnits()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            Check.False(File.Exists(ApeActOf(fx)), "a stock install has no ape.act");

            PatchState want = Enabled(session);
            want.Tier2Depth = 180;
            want.Leakage1 = 6;  want.Leakage2 = 30;
            want.FloodThreshold = 45; want.MotorDamage = 12.5;
            want.HullMult1 = 2.0; want.HullMult2 = 3.5;
            WriteReport report = session.Apply(want);
            Check.True(report.Ok, report.Message);
            Check.True(File.Exists(ApeActOf(fx)), "ape.act deployed");

            byte[] deployed = File.ReadAllBytes(ApeActOf(fx));
            byte[] template = fx.ApeActTemplate();
            Check.Equal(template.Length, deployed.Length, "a copy of the template");
            Check.Equal(180f, FieldIn(deployed, fx, "tier2_depth_m"), "tier-2 depth, metres");
            Check.Equal((float)(6.0 / 6000.0), FieldIn(deployed, fx, "t1_seep_per_s"), "6 %/min");
            Check.Equal((float)(30.0 / 6000.0), FieldIn(deployed, fx, "t2_seep_per_s"), "30 %/min");
            Check.Equal(0.45f, FieldIn(deployed, fx, "flood_threshold"), "45 % flood");
            Check.Equal(0.125f, FieldIn(deployed, fx, "motor_damage_per_s"), "12.5 %/game-s");
            Check.Equal(2.0f, FieldIn(deployed, fx, "t1_hull_mult"), "x2");
            Check.Equal(3.5f, FieldIn(deployed, fx, "t2_hull_mult"), "x3.5");

            // Outside the struct's fields, not a byte moved: marker and version kept.
            ApeActStruct s = Struct(fx);
            int differing = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (i >= s.Offset + s.FieldsStart && i < s.Offset + s.Size) continue;
                if (template[i] != deployed[i]) differing++;
            }
            Check.Equal(0, differing, "only the fields differ from the template");
            Check.Contains("Depth stress (ape.act deployed)", report.Message, "the confirmation names it");
        }

        // -- reading back ------------------------------------------------------

        public static void Test_ReopeningReadsTheValuesBackFromTheDeployedFile()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Enabled(session);
            want.Tier2Depth = 220;
            want.Leakage1 = 2.5; want.Leakage2 = 0;
            want.FloodThreshold = 72.5; want.MotorDamage = 33.3;
            want.HullMult1 = 1.0; want.HullMult2 = 4.75;
            Check.True(session.Apply(want).Ok, "applying");

            PatchSession after = fx.Session();
            PatchState got = after.Current;
            Check.True(after.Install.Applied(DepthStress.PatchId), "reads as applied");
            Check.True(got.DepthApplied, "the checkbox opens checked");
            Check.Equal(220.0, got.Tier2Depth, "tier-2 depth");
            Check.Equal(2.5, got.Leakage1, "tier-1 leakage");
            Check.Equal(0.0, got.Leakage2, "tier-2 leakage");
            Check.Equal(72.5, got.FloodThreshold, "flooding threshold");
            Check.Equal(33.3, got.MotorDamage, "motor damage");
            Check.Equal(1.0, got.HullMult1, "tier-1 multiplier");
            Check.Equal(4.75, got.HullMult2, "tier-2 multiplier");
            Check.True(after.Plan(got.Clone()).Empty, "and applying it again changes nothing");
        }

        public static void Test_WithNoDeployedFileTheTemplatesDefaultsAreShown()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            // A template whose defaults differ from PatchState's, so the values
            // shown can only have come from it.
            byte[] template = (byte[])fx.ApeActTemplate().Clone();
            ApeActStruct s = Struct(fx);
            BitConverter.GetBytes(175f).CopyTo(template, s.Offset + s.FieldAt("tier2_depth_m"));
            BitConverter.GetBytes(2.0f).CopyTo(template, s.Offset + s.FieldAt("t2_hull_mult"));
            fx.Template = template;

            PatchSession session = fx.Session();
            Check.False(session.Install.Applied(DepthStress.PatchId), "nothing deployed");
            Check.False(session.Current.DepthApplied, "unchecked");
            Check.Equal(175.0, session.Current.Tier2Depth, "the template's tier-2 depth");
            Check.Equal(2.0, session.Current.HullMult2, "the template's tier-2 multiplier");
        }

        public static void Test_AFreshStateHoldsTheLeakageDefaults()
        {
            PatchState d = new PatchState();
            Check.Equal(0.35, d.Leakage1, "0.35 %/min (owner, 2026-11-25)");
            Check.Equal(0.6, d.Leakage2, "0.6 %/min (owner, 2026-11-25)");
        }

        public static void Test_AFreshStateHoldsTheMotorFloodingDefaults()
        {
            PatchState d = new PatchState();
            Check.Equal(20.0, d.FloodThreshold, "20 % flood (Stoss)");
            Check.Equal(10.0, d.MotorDamage, "10 %/game-s (Stoss)");
        }

        public static void Test_TheShippedTemplateHoldsTheCurrentDefaults()
        {
            PatchSession session = Fixtures.Stock(true).Session();
            PatchState d = session.Current;
            Check.Equal(150.0, d.Tier2Depth, "150 m");
            Check.Equal(0.35, d.Leakage1, "0.35 %/min (owner, 2026-11-25)");
            Check.Equal(0.6, d.Leakage2, "0.6 %/min (owner, 2026-11-25)");
            Check.Equal(20.0, d.FloodThreshold, "20 % flood (Stoss)");
            Check.Equal(10.0, d.MotorDamage, "10 %/game-s (ticket 31)");
            Check.Equal(1.25, d.HullMult1, "x1.25");
            Check.Equal(1.5, d.HullMult2, "x1.5");
        }

        public static void Test_ChangingAValueRewritesTheDeployedFile()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession first = fx.Session();
            Check.True(first.Apply(Enabled(first)).Ok, "first apply");

            PatchSession second = fx.Session();
            PatchState want = second.Current.Clone();
            want.HullMult2 = 2.25;
            WriteReport report = second.Apply(want);
            Check.True(report.Ok, report.Message);
            Check.Equal(2.25f, FieldIn(File.ReadAllBytes(ApeActOf(fx)), fx, "t2_hull_mult"), "rewritten");
        }

        // -- removing ----------------------------------------------------------

        public static void Test_DisablingDeletesApeAct()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            Check.True(session.Apply(Enabled(session)).Ok, "deploying");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.DepthApplied = false;
            WriteReport report = applied.Apply(off);
            Check.True(report.Ok, report.Message);
            Check.False(File.Exists(ApeActOf(fx)), "ape.act removed");
            Check.Contains("Depth stress (ape.act removed)", report.Message, "and the confirmation says so");

            PatchSession after = fx.Session();
            Check.False(after.Install.Applied(DepthStress.PatchId), "reads as not applied");
            Check.True(after.Plan(after.Current.Clone()).Empty, "and nothing is left to do");
        }

        public static void Test_WithNoRuntimeFeatureEnabledNoApeActIsLeft()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            Fixtures.AddUBoat(fx, "NSS_Uboat7c", "YES", 500f, 0.08f);
            PatchSession session = fx.Session();
            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.DieselApplied = true;
            want.CrushApplied = true;
            WriteReport report = session.Apply(want);
            Check.True(report.Ok, report.Message);
            Check.False(File.Exists(ApeActOf(fx)), "only static features: no ape.act");
        }

        public static void Test_TheCrushTimePatchAppliesAndRevertsWithoutApeAct()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string zon = Fixtures.AddUBoat(fx, "NSS_Uboat7c", "YES", 500f, 0.08f);
            byte[] before = File.ReadAllBytes(zon);

            PatchSession session = fx.Session();
            PatchState crush = session.Current.Clone();
            crush.CrushApplied = true;
            crush.CrushTime = 20;
            Check.True(session.Apply(crush).Ok, "crush time on its own");
            Check.Equal(25f, Fixtures.CrashSpeedIn(zon), "500 / 20");
            Check.False(File.Exists(ApeActOf(fx)), "no ape.act");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.CrushApplied = false;
            Check.True(applied.Apply(off).Ok, "reverting it");
            Check.Bytes(before, File.ReadAllBytes(zon), "the .zon is back");
            Check.False(File.Exists(ApeActOf(fx)), "still no ape.act");
        }

        // -- refusing ----------------------------------------------------------

        public static void Test_AWrongMarkerInTheTemplateRefusesTheApplyAndWritesNothing()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] template = (byte[])fx.ApeActTemplate().Clone();
            template[Struct(fx).Offset + 3] ^= 0x20;
            fx.Template = template;
            byte[] exeBefore = fx.ExeBytes();

            PatchSession session = fx.Session();
            PatchState want = Enabled(session);
            want.HullApplied = true;       // a write that must not happen either
            WriteReport report = session.Apply(want);
            Check.False(report.Ok, "refused");
            Check.Contains("marker", report.Message, "naming the marker");
            Check.Contains("Nothing was written", report.Message, "and saying nothing was written");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe untouched");
            Check.False(File.Exists(ApeActOf(fx)), "no ape.act");
        }

        public static void Test_AWrongVersionInTheTemplateRefusesTheApply()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] template = (byte[])fx.ApeActTemplate().Clone();
            ApeActStruct s = Struct(fx);
            BitConverter.GetBytes(s.Version + 1).CopyTo(template, s.Offset + s.Magic.Length);
            fx.Template = template;

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Enabled(session));
            Check.False(report.Ok, "refused");
            Check.Contains("version", report.Message, "naming the version");
            Check.False(File.Exists(ApeActOf(fx)), "no ape.act");
        }

        public static void Test_AWrongOffsetInTheManifestRefusesTheApplyAndWritesNothing()
        {
            foreach (int shift in new int[] { 4, -4, 1 << 30 })
            {
                Fixtures.Install fx = Fixtures.Stock(true);
                fx.Manifest.DepthStressStruct.Offset += shift;
                byte[] exeBefore = fx.ExeBytes();

                PatchSession session = fx.Session();
                PatchState want = Enabled(session);
                want.HullApplied = true;
                WriteReport report = session.Apply(want);
                Check.False(report.Ok, "refused, offset shifted by " + Fmt.Int(shift));
                Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe untouched");
                Check.False(File.Exists(ApeActOf(fx)), "no ape.act");
            }
        }

        // -- rollback ----------------------------------------------------------

        public static void Test_AnApeActWriteFailureRollsBackEveryStore()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string zon = Fixtures.AddUBoat(fx, "NSS_Uboat7c", "YES", 500f, 0.08f);
            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();
            string menuBefore = fx.MenuTextContent();
            byte[] zonBefore = File.ReadAllBytes(zon);

            PatchSession session = fx.Session();
            session.WriteApeActBytes = delegate(string path, byte[] bytes)
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 });   // a partial file
                throw new IOException("disk full");
            };
            PatchState want = Enabled(session);
            want.HullApplied = true;
            want.DieselApplied = true;
            want.CrushApplied = true;
            WriteReport report = session.Apply(want);

            Check.False(report.Ok, "the commit fails");
            Check.Contains("disk full", report.Message, "naming the failure");
            Check.Equal(0, report.SitesWritten.Count, "and claims nothing written");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe put back");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini put back");
            Check.Equal(menuBefore, fx.MenuTextContent(), "en_menu.txt put back");
            Check.Bytes(zonBefore, File.ReadAllBytes(zon), "the .zon put back");
            Check.False(File.Exists(zon + ".ape-orig"), "and its copy removed");
            Check.False(File.Exists(ApeActOf(fx)), "the partial ape.act removed");
        }

        public static void Test_AFailedRewriteOfApeActPutsTheOldOneBack()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession first = fx.Session();
            Check.True(first.Apply(Enabled(first)).Ok, "deploying");
            byte[] deployed = File.ReadAllBytes(ApeActOf(fx));

            PatchSession second = fx.Session();
            Action<string, byte[]> real = second.WriteApeActBytes;
            bool failed = false;
            second.WriteApeActBytes = delegate(string path, byte[] bytes)
            {
                if (!failed) { failed = true; File.WriteAllBytes(path, new byte[] { 9 }); throw new IOException("locked"); }
                real(path, bytes);
            };
            PatchState want = second.Current.Clone();
            want.HullMult1 = 3.0;
            Check.False(second.Apply(want).Ok, "the commit fails");
            Check.Bytes(deployed, File.ReadAllBytes(ApeActOf(fx)), "the previous ape.act is back");
        }

        public static void Test_AFailedDeleteOfApeActRollsBackEveryStore()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession first = fx.Session();
            PatchState on = Enabled(first);
            on.HullApplied = true;
            Check.True(first.Apply(on).Ok, "deploying, with hull repair");
            byte[] exeApplied = fx.ExeBytes();
            byte[] deployed = File.ReadAllBytes(ApeActOf(fx));

            PatchSession second = fx.Session();
            Action<string> real = second.DeleteFile;
            string apeAct = ApeActOf(fx);
            second.DeleteFile = delegate(string path)
            {
                if (path == apeAct) throw new IOException("in use");
                real(path);
            };
            PatchState off = second.Current.Clone();
            off.DepthApplied = false;
            off.HullApplied = false;
            WriteReport report = second.Apply(off);
            Check.False(report.Ok, "the commit fails");
            Check.Contains("in use", report.Message, "naming the failure");
            Check.Bytes(exeApplied, fx.ExeBytes(), "sh3.exe still holds hull repair");
            Check.Bytes(deployed, File.ReadAllBytes(ApeActOf(fx)), "ape.act still there");
        }

        public static void Test_AnEarlierStoresFailureLeavesNoApeAct()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string zon = Fixtures.AddUBoat(fx, "NSS_Uboat7c", "YES", 500f, 0.08f);
            PatchSession session = fx.Session();
            session.WriteZonBytes = delegate(string path, byte[] bytes)
            {
                throw new IOException("disk full");
            };
            PatchState want = Enabled(session);
            want.CrushApplied = true;
            Check.False(session.Apply(want).Ok, "the commit fails");
            Check.False(File.Exists(ApeActOf(fx)), "no ape.act");
            Check.False(File.Exists(zon + ".ape-orig"), "no .zon copy");
        }

        // -- ranges and neutral values -----------------------------------------

        public static void Test_EachFieldIsCheckedAgainstItsOwnRange()
        {
            // (field, a value just inside each end, a value just outside each end)
            Action<PatchState, double>[] set =
            {
                delegate(PatchState s, double v) { s.Tier2Depth = v; },
                delegate(PatchState s, double v) { s.Leakage1 = v; },
                delegate(PatchState s, double v) { s.Leakage2 = v; },
                delegate(PatchState s, double v) { s.FloodThreshold = v; },
                delegate(PatchState s, double v) { s.MotorDamage = v; },
                delegate(PatchState s, double v) { s.HullMult1 = v; },
                delegate(PatchState s, double v) { s.HullMult2 = v; },
            };
            double[][] ok =
            {
                new double[] { 101, 300 }, new double[] { 0, 60 }, new double[] { 0, 60 },
                new double[] { 5, 95 }, new double[] { 0, 100 },
                new double[] { 1.0, 5.0 }, new double[] { 1.0, 5.0 },
            };
            double[][] bad =
            {
                new double[] { 100, 300.5 }, new double[] { -0.1, 60.1 }, new double[] { -0.1, 60.1 },
                new double[] { 4.9, 95.1 }, new double[] { -0.1, 100.1 },
                new double[] { 0.99, 5.01 }, new double[] { 0.99, 5.01 },
            };

            Fixtures.Install fx = Fixtures.Stock(true);
            for (int f = 0; f < set.Length; f++)
            {
                foreach (double v in bad[f])
                {
                    PatchSession session = fx.Session();
                    PatchState want = Enabled(session);
                    set[f](want, v);
                    WriteReport report = session.Apply(want);
                    Check.False(report.Ok, "field " + Fmt.Int(f) + " = " + Fmt.F2(v) + " is refused");
                    Check.Contains("depth stress", report.Message, "and named");
                    Check.False(File.Exists(ApeActOf(fx)), "nothing deployed");
                }
                foreach (double v in ok[f])
                {
                    PatchSession session = fx.Session();
                    PatchState want = Enabled(session);
                    set[f](want, v);
                    WriteReport report = session.Apply(want);
                    Check.True(report.Ok, "field " + Fmt.Int(f) + " = " + Fmt.F2(v) + ": " + report.Message);
                    File.Delete(ApeActOf(fx));
                }
            }
        }

        public static void Test_AMilderTier2ThanTier1IsAccepted()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Enabled(session);
            want.Leakage1 = 20; want.Leakage2 = 1;
            want.HullMult1 = 4.0; want.HullMult2 = 1.1;
            WriteReport report = session.Apply(want);
            Check.True(report.Ok, report.Message);
        }

        public static void Test_NeutralValuesSwitchEachEffectOffInTheStruct()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Enabled(session);
            want.Leakage1 = 0; want.Leakage2 = 0;
            want.MotorDamage = 0;
            want.HullMult1 = 1.0; want.HullMult2 = 1.0;
            Check.True(session.Apply(want).Ok, "applying");

            byte[] deployed = File.ReadAllBytes(ApeActOf(fx));
            foreach (string f in new string[] { "t1_seep_per_s", "t2_seep_per_s", "motor_damage_per_s" })
                Check.Equal(0f, FieldIn(deployed, fx, f), f + " is 0");
            Check.Equal(1f, FieldIn(deployed, fx, "t1_hull_mult"), "tier-1 multiplier 1.0");
            Check.Equal(1f, FieldIn(deployed, fx, "t2_hull_mult"), "tier-2 multiplier 1.0");
        }
    }
}
