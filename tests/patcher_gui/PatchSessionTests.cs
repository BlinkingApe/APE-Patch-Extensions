// PatchSession is the tool's only seam: everything that touches a file is behind
// it, and the WinForms layer holds no logic of its own.  So these cases open a
// prepared game directory and assert what the tool reports, then apply a desired
// state and assert what the bytes and the ini text became.  Nothing here reaches
// for a private method or asserts that a particular write helper was called.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class PatchSessionTests
    {
        static PatchState Both(PatchSession session)
        {
            PatchState s = session.Current.Clone();
            s.HullApplied = true;
            s.DieselApplied = true;
            return s;
        }

        // -- 1. detection ------------------------------------------------------

        public static void Test_ASupportedInstallIsReportedSupported()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            Check.True(session.Install.Supported, "the fixture build is admitted");
            Check.Equal("OAE 1.5.2", session.Install.BuildName, "build name");
            Check.Equal("v 1.6E1", session.Install.HsieVersion, "h.sie version, read from the binary");
            Check.Equal(fx.Dir, session.Install.InstallPath, "the directory it opened");
        }

        public static void Test_AnUnrecognisedBuildIsRefusedAndNamesWhatIsSupported()
        {
            Fixtures.Install fx = Fixtures.UnrecognisedBuild();
            PatchSession session = fx.Session();

            Check.False(session.Install.Supported, "a mutated sh3.exe is not admitted");
            Check.Contains("OAE 1.5.2", session.Install.SupportedBuilds,
                "the refusal says which build APE does support");
            Check.Contains("v 1.6E1", session.Install.SupportedBuilds, "and which h.sie version");
        }

        public static void Test_AnUnsupportedBuildRefusesToWriteAnything()
        {
            Fixtures.Install fx = Fixtures.UnrecognisedBuild();
            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is untouched");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini is untouched");
        }

        public static void Test_TheDieselFlagDecidesWhetherTheFeatureIsAvailable()
        {
            // h.sie's own Diesel Damage switch is 0x00 in every surveyed install,
            // so disabled-with-explanation is the state a real user meets first.
            Fixtures.Install off = Fixtures.Stock(false);
            PatchSession sOff = PatchSession.Open(off.Dir, off.Manifest);
            Check.False(sOff.Install.DieselFlagOn, "flag 0x00 is read as off");
            Check.False(sOff.Install.Available(Manifest.DieselPatchId), "so the feature is unavailable");
            Check.True(sOff.Install.Available(Manifest.HullPatchId), "hull repair does not depend on it");

            Fixtures.Install on = Fixtures.Stock(true);
            PatchSession sOn = PatchSession.Open(on.Dir, on.Manifest);
            Check.True(sOn.Install.DieselFlagOn, "flag 0xFF is read as on");
            Check.True(sOn.Install.Available(Manifest.DieselPatchId), "and the feature is available");
        }

        public static void Test_AnUnavailableFeatureIsNeverWritten()
        {
            Fixtures.Install fx = Fixtures.Stock(false);   // h.sie's diesel switch off
            PatchSession session = fx.Session();

            PatchState want = Both(session);               // asks for diesel anyway
            want.HullMin = 30;
            want.HullMax = 50;
            WriteReport report = session.Apply(want);

            Check.True(report.Ok, "hull repair still commits");
            Check.Equal(2, report.SitesWritten.Count, "only hull repair's two sites");
            PatchSession after = fx.Session();
            Check.False(after.Install.Applied(Manifest.DieselPatchId),
                "diesel damage was left entirely alone");
            Check.Equal("Val9=2e-4", IniLine(fx, "Val9"), "hsie.ini was not touched for it");
        }

        // -- 1b. admitting a second build (umbrella ticket 01 stories 16-19) ---

        public static void Test_ASecondAdmittedBuildAppliesAndRevertsByteForByte()
        {
            // The same round trip Test_UncheckingBothFeaturesRestoresBothFilesByteForByte
            // covers for the shipped v 1.6E1 build, run against a build stamped
            // v 1.6B1 (KC 2.5's h.sie version) and admitted as its own allowlist
            // entry sharing the shipped build's offsets -- ticket 01 story 18.
            Fixtures.Install fx = Fixtures.StockBuild(true, "KC 2.5", "v 1.6B1");
            Check.True(fx.Session().Install.Supported, "the second build is admitted");
            Check.Equal("KC 2.5", fx.Session().Install.BuildName, "and named correctly");
            Check.Equal("v 1.6B1", fx.Session().Install.HsieVersion, "reading its own h.sie stamp");

            byte[] exeBefore = fx.ExeBytes();
            byte[] iniBefore = File.ReadAllBytes(fx.HsieIni);

            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.Rate = 4.0;
            Check.True(session.Apply(want).Ok, "applying against the second build");
            Check.False(Same(exeBefore, fx.ExeBytes()), "sh3.exe really changed");

            PatchSession applied = fx.Session();
            PatchState offAgain = applied.Current.Clone();
            offAgain.HullApplied = false;
            offAgain.DieselApplied = false;
            Check.True(applied.Apply(offAgain).Ok, "unchecking both features");

            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is byte-identical to the start");
            Check.Bytes(iniBefore, File.ReadAllBytes(fx.HsieIni), "hsie.ini is byte-identical too");
        }

        public static void Test_SharingAnHsieVersionDoesNotShareAdmission()
        {
            // Ticket 01 story 19: KC 2.5 and OAE 1.5.3.7 both report h.sie
            // "v 1.6B1", but they are different builds and admitting one must not
            // admit the other by side effect.
            Fixtures.Install[] builds = Fixtures.TwoBuildsSharingAnHsieVersion();
            Fixtures.Install kc = builds[0];
            Fixtures.Install oae1537 = builds[1];

            Check.True(kc.Session().Install.Supported, "KC 2.5 is admitted on its own identity");
            Check.Equal("KC 2.5", kc.Session().Install.BuildName, "and identified as itself");

            Check.True(oae1537.Session().Install.Supported,
                "OAE 1.5.3.7 is admitted on its own identity, separately");
            Check.Equal("OAE 1.5.3.7", oae1537.Session().Install.BuildName, "and identified as itself");
        }

        public static void Test_AnUnsupportedBuildRefusalNamesEveryAdmittedBuild()
        {
            // Ticket 01 story 17: a refusal keeps naming every build APE does
            // support, not just the first one admitted.
            Fixtures.Install[] builds = Fixtures.TwoBuildsSharingAnHsieVersion();
            Fixtures.Install fx = builds[0];
            byte[] exe = fx.ExeBytes();
            exe[0x1000] ^= 0xFF;                // nowhere near any site
            File.WriteAllBytes(fx.Sh3Exe, exe);

            InstallReport install = fx.Session().Install;
            Check.False(install.Supported, "a mutated sh3.exe is not admitted");
            Check.Contains("KC 2.5", install.SupportedBuilds, "names the first admitted build");
            Check.Contains("OAE 1.5.3.7", install.SupportedBuilds, "and the second");
        }

        // -- 2. read-back ------------------------------------------------------

        public static void Test_AStockInstallReportsBothFeaturesUnapplied()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            Check.False(session.Install.Applied(Manifest.HullPatchId), "hull repair is h.sie's");
            Check.False(session.Install.Applied(Manifest.DieselPatchId), "diesel damage is h.sie's");
            Check.False(session.Current.HullApplied, "and the model opens unchecked");
            Check.False(session.Current.DieselApplied, "for both features");
            Check.Equal(0, session.Current.HullMin, "hull repair opens on APE's own default (ticket 04)");
            Check.Equal(5, session.Current.HullMax, "not h.sie's stock 5-20 roll it has not yet applied");
        }

        public static void Test_AnAppliedInstallOpensWithTheValuesItHolds()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState want = Both(session);
            want.HullMin = 12;
            want.HullMax = 34;
            want.Rate = 2.5;
            want.BandLo = 0.20;
            want.BandHi = 0.45;
            Check.True(session.Apply(want).Ok, "the commit lands");

            PatchSession reopened = fx.Session();
            Check.True(reopened.Install.Applied(Manifest.HullPatchId), "hull repair reads as applied");
            Check.True(reopened.Install.Applied(Manifest.DieselPatchId), "diesel damage reads as applied");

            PatchState back = reopened.Current;
            Check.True(back.HullApplied, "the hull checkbox opens ticked");
            Check.Equal(12, back.HullMin, "with the minimum that was written");
            Check.Equal(34, back.HullMax, "and the maximum");
            Check.True(back.DieselApplied, "the diesel checkbox opens ticked");
            Check.Close(2.5, back.Rate, 0.05, "with the rate that was written");
            Check.Close(0.20, back.BandLo, 1e-6, "and the band's low end");
            Check.Close(0.45, back.BandHi, 1e-6, "and its high end");
        }

        public static void Test_TheDieselPanelsTwoFieldsComeFromTwoDifferentStores()
        {
            // The rate lives in hsie.ini (it is inside the .pconst clobber region,
            // ticket 09); the band lives in sh3.exe .pcode.  Prove the read-back
            // really spans both by corrupting one store at a time.
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.Rate = 3.0;
            want.BandLo = 0.20;
            want.BandHi = 0.60;
            Check.True(session.Apply(want).Ok, "the commit lands");

            // Rewind hsie.ini alone: the rate goes back to h.sie's, the band stays.
            File.Copy(Fixtures.HsieIniPath(), fx.HsieIni, true);
            PatchState back = fx.Session().Current;
            Check.Close(0.20, back.BandLo, 1e-6, "the band still comes from sh3.exe");
            Check.Close(0.60, back.BandHi, 1e-6, "both ends of it");
            Check.Close(7200.0, back.Rate, 1.0, "the rate came from hsie.ini and is h.sie's 2e-4 again");
        }

        // -- 3. round-trip revert ---------------------------------------------

        public static void Test_UncheckingBothFeaturesRestoresBothFilesByteForByte()
        {
            // This is the test that stands in for the backup logic APE does not
            // have.  Unchecking is the uninstaller, so it must be exact.
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] exeBefore = fx.ExeBytes();
            byte[] iniBefore = File.ReadAllBytes(fx.HsieIni);

            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.HullMin = 40;
            want.HullMax = 60;
            want.Rate = 9.9;
            want.BandLo = 0.30;
            want.BandHi = 0.70;
            Check.True(session.Apply(want).Ok, "applying both features");
            Check.False(Same(exeBefore, fx.ExeBytes()), "sh3.exe really did change");
            Check.False(Same(iniBefore, File.ReadAllBytes(fx.HsieIni)), "hsie.ini really did change");

            PatchSession applied = fx.Session();
            PatchState offAgain = applied.Current.Clone();
            offAgain.HullApplied = false;
            offAgain.DieselApplied = false;
            Check.True(applied.Apply(offAgain).Ok, "unchecking both features");

            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is byte-identical to the start");
            Check.Bytes(iniBefore, File.ReadAllBytes(fx.HsieIni), "hsie.ini is byte-identical too");
        }

        public static void Test_TheFixedRewriteIsWrittenAndRestored()
        {
            // 0x635818 has no user-facing field at all: it is written whole when
            // diesel damage is on and restored from its stored original run when
            // it is off.
            Fixtures.Install fx = Fixtures.Stock(true);
            Site helper = Find(fx.Manifest, Manifest.DieselPatchId, "tc-helper-rewrite");
            int at = helper.OffsetFor(Fixtures.TestVersionId);

            PatchSession session = fx.Session();
            Check.Bytes(helper.Original, Slice(fx.ExeBytes(), at, helper.Original.Length),
                "stock: h.sie's 1-(1-p)^TC helper");

            Check.True(session.Apply(Both(session)).Ok, "apply");
            Check.Bytes(helper.Replacement, Slice(fx.ExeBytes(), at, helper.Replacement.Length),
                "applied: APE's clamped p*TC, int3-padded to the same length");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.DieselApplied = false;
            Check.True(applied.Apply(off).Ok, "uncheck");
            Check.Bytes(helper.Original, Slice(fx.ExeBytes(), at, helper.Original.Length),
                "reverted: h.sie's helper is back");
        }

        // -- 4. ini surgery through the session --------------------------------

        public static void Test_ApplyingDieselDamageWritesOnlyTheEightKeysItOwns()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string before = fx.IniText();

            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.Rate = 1.5;
            Check.True(session.Apply(want).Ok, "apply");

            IniFile after = IniFile.Load(fx.HsieIni);
            Check.Equal("4.1667e-08", after.Value("Val9"), "the rate, in the form h.sie's parser expects");
            Check.Equal("4.0718e-08", after.Value("Val10"), "the derived idle cutoff");
            Check.Equal("1.0", after.Value("Val11"), "factors flattened");
            Check.Equal("1.0", after.Value("Val12"), "factors flattened");
            Check.Equal("1.0", after.Value("Val13"), "factors flattened");
            Check.Equal("0.01", after.Value("Val14"), "the exponent that carries the idle cutoff");
            Check.Equal("0.01", after.Value("Val15"), "the exponent that carries the idle cutoff");
            Check.Equal("0.01", after.Value("Val16"), "the exponent that carries the idle cutoff");

            IniFile original = IniFile.Load(Fixtures.HsieIniPath());
            foreach (string key in original.Keys())
            {
                if (key.StartsWith("Val", StringComparison.Ordinal))
                {
                    int n;
                    if (int.TryParse(key.Substring(3), out n) && n >= 9 && n <= 16) continue;
                }
                Check.Equal(original.Line(key), after.Line(key), key + " is untouched");
            }

            Check.Contains("; p_dso = Basic damage probability for every second", fx.IniText(),
                "h.sie's comments survive the write");
            Check.Contains("NEVER CHANGE", fx.IniText(), "and his banners");
            Check.Equal(CountNewlines(before), CountNewlines(fx.IniText()),
                "no line was added or dropped");
        }

        public static void Test_TheDerivedThresholdFollowsTheRateOnEveryWrite()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.Rate = 4.0;
            Check.True(session.Apply(want).Ok, "apply");

            IniFile ini = IniFile.Load(fx.HsieIni);
            double pdso, threshold;
            Check.True(ini.TryNumber("Val9", out pdso), "Val9 parses");
            Check.True(ini.TryNumber("Val10", out threshold), "Val10 parses");
            Check.Close(want.Pdso, pdso, want.Pdso * 1e-4, "Val9 is p_dso");
            Check.Close(pdso * 0.977237, threshold, pdso * 1e-4, "Val10 trails it by the idle factor");
            Check.True(threshold < pdso, "and stays below it, or the mod is inert");
        }

        // -- 5. verify before write -------------------------------------------

        public static void Test_ACorruptSh3ExeSiteAbortsTheWholeCommit()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            Site band = Find(fx.Manifest, Manifest.DieselPatchId, "band-first-hi");
            byte[] exe = fx.ExeBytes();
            exe[band.OffsetFor(Fixtures.TestVersionId)] = 0x90;   // the push opcode -- context, not field
            File.WriteAllBytes(fx.Sh3Exe, exe);

            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.Contains("0x6351B0", report.Message, "the refusal names the offending site");
            Check.Contains("90", report.Message, "and says what it found there");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is untouched -- not half-patched");
            Check.Equal(iniBefore, fx.IniText(), "and hsie.ini too, across the store boundary");
        }

        public static void Test_AMissingHsieIniKeyAbortsTheWholeCommit()
        {
            // The other store: the all-or-nothing guarantee has to hold across the
            // sh3.exe / hsie.ini boundary, not just within one file.
            Fixtures.Install fx = Fixtures.Stock(true);
            string text = fx.IniText().Replace("Val13=8.0", "Vxl13=8.0");
            File.WriteAllBytes(fx.HsieIni, System.Text.Encoding.GetEncoding(28591).GetBytes(text));

            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.Contains("Val13", report.Message, "the refusal names the offending site");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is untouched, even though its sites were fine");
            Check.Equal(iniBefore, fx.IniText(), "and hsie.ini is untouched");
        }

        public static void Test_AnEditedFieldStillVerifiesBecauseOnlyContextIsChecked()
        {
            // Verification checks a site's invariant context, never its mutable
            // field -- that is what lets a user-edited value coexist with
            // verify-before-write (ticket 02).
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession first = fx.Session();
            PatchState want = Both(first);
            want.BandHi = 0.44;
            Check.True(first.Apply(want).Ok, "first commit");

            PatchSession second = fx.Session();
            PatchState again = second.Current.Clone();
            again.BandHi = 0.66;
            Check.True(second.Apply(again).Ok, "a second commit over APE's own values still verifies");
            Check.Close(0.66, fx.Session().Current.BandHi, 1e-6,
                "and lands the new value");
        }

        public static void Test_AMissingGameFileIsReportedNotCrashed()
        {
            string dir = Fixtures.TempDir();
            PatchSession session = PatchSession.Open(dir, Manifest.Embedded());
            Check.False(session.Install.Supported, "an empty directory is not an install");
            Check.Contains("sh3.exe", session.Install.RefusalReason, "and says what is missing");
        }

        // -- 6. the write report ----------------------------------------------

        public static void Test_TheWriteReportNamesEverySiteItTouched()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.HullMin = 10;
            want.HullMax = 30;
            WriteReport report = session.Apply(want);

            Check.True(report.Ok, "the commit lands");
            // 2 hull sites + 8 ini keys + 4 band immediates + 1 rewrite + the
            // diesel-message gate + its text, less Val11: h.sie already ships
            // Factor-for-0-damaged as 1.0, which is what APE wants, so that
            // site is not a change and is not claimed.
            Check.Equal(16, report.SitesWritten.Count, "every site that actually changes is named");
            Check.DoesNotContain("Val11", string.Join("|", report.SitesWritten.ToArray()),
                "and nothing that did not");
            Check.Contains("hsie.ini Val9", string.Join("|", report.SitesWritten.ToArray()),
                "including the ini keys");
            Check.Contains("0x635818", string.Join("|", report.SitesWritten.ToArray()),
                "and the fixed rewrite");
            Check.Contains("data/Menu/en_menu.txt 4893", string.Join("|", report.SitesWritten.ToArray()),
                "and the diesel damage report's text");
        }

        public static void Test_HullRepairAtHsiesOwnRangeIsNotAChange()
        {
            // h.sie's own 5-20 roll is still a no-op to apply *at those exact
            // numbers* -- ticket 04 moved APE's shipped default to 0-5, a real
            // narrowing, so this now has to construct h.sie's stock range
            // explicitly rather than rely on Current already holding it.
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.HullMin = 5;
            want.HullMax = 20;            // h.sie's own stock roll, exactly what the install holds
            WriteReport report = session.Apply(want);

            Check.True(report.Ok, "nothing wrong with it");
            Check.Equal(0, report.SitesWritten.Count, "but there was nothing to write");
        }

        public static void Test_ApplyingNothingWritesNothing()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(session.Current.Clone());

            Check.True(report.Ok, "there is nothing wrong with changing nothing");
            Check.Equal(0, report.SitesWritten.Count, "and nothing was written");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe untouched");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini untouched");
        }

        // -- 7. the rate ceiling, enforced at the seam (ticket 01 story 3) -----

        public static void Test_ARateAtTheCeilingIsAcceptedAndBeyondItIsRefused()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();

            PatchState atCeiling = Both(session);
            atCeiling.Rate = PatchState.RateCeiling;
            Check.True(session.Apply(atCeiling).Ok, "the ceiling itself is accepted");

            PatchSession second = fx.Session();
            PatchState overCeiling = second.Current.Clone();
            overCeiling.DieselApplied = true;
            overCeiling.Rate = PatchState.RateCeiling + 1.0;
            WriteReport report = second.Apply(overCeiling);
            Check.False(report.Ok, "just above the ceiling is refused");
            Check.Contains("360000", report.Message, "the refusal names the limit");
        }

        // -- 8. commit atomicity (ticket 01 stories 4-6, 14) --------------------

        public static void Test_AnHsieIniWriteFailureLeavesSh3ExeByteIdentical()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            session.SaveIni = delegate { throw new IOException("hsie.ini is read-only"); };
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.Contains("hsie.ini", report.Message, "and says which file failed");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe was rolled back to byte-identical");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini was never touched");
        }

        public static void Test_AnHsieIniWriteFailureAndARollbackFailureReportsBothFilesAndRecovery()
        {
            // The case the header comment used to promise could not happen: the
            // ini write fails, and putting sh3.exe back fails too. The player
            // must be told both files' actual states rather than seeing only the
            // ini error while sh3.exe sits silently patched.
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] exeBefore = fx.ExeBytes();

            PatchSession session = fx.Session();
            int exeWrites = 0;
            session.WriteExeBytes = delegate(string path, byte[] bytes)
            {
                exeWrites++;
                if (exeWrites == 1) File.WriteAllBytes(path, bytes);   // the real, forward write
                else throw new IOException("disk went away mid-rollback");   // the rollback
            };
            session.SaveIni = delegate { throw new IOException("hsie.ini is read-only"); };

            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit does not claim success");
            Check.Equal(0, report.SitesWritten.Count, "and claims no sites written");
            Check.Contains("sh3.exe", report.Message, "the report names sh3.exe");
            Check.Contains("hsie.ini", report.Message, "and hsie.ini");
            Check.Contains("inconsistent", report.Message, "and says the install is inconsistent");
            Check.Contains("To recover", report.Message, "and tells the player how to recover");
            Check.False(Same(exeBefore, fx.ExeBytes()),
                "sh3.exe really is left holding APE's change -- the rollback truly failed");
        }

        // -- 9. foreign-write detection on hsie.ini (ticket 01 stories 7-9, 12) -

        public static void Test_AHandEditedHsieIniValueIsRefusedAndNamesTheKeyAndBothValues()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string text = fx.IniText().Replace("Val9=2e-4", "Val9=9.9e-4");
            File.WriteAllBytes(fx.HsieIni, System.Text.Encoding.GetEncoding(28591).GetBytes(text));

            byte[] exeBefore = fx.ExeBytes();
            string iniBefore = fx.IniText();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.True(report.ForeignWrite, "and flagged as the overridable foreign-write refusal");
            Check.Contains("Val9", report.Message, "the refusal names the key");
            Check.Contains("9.9e-4", report.Message, "and shows the value found");
            Check.Contains("2e-4", report.Message, "and h.sie's original");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is untouched");
            Check.Equal(iniBefore, fx.IniText(), "hsie.ini is untouched, including the hand-edit");
        }

        public static void Test_TheForeignWriteRefusalCanBeOverriddenDeliberately()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string text = fx.IniText().Replace("Val9=2e-4", "Val9=9.9e-4");
            File.WriteAllBytes(fx.HsieIni, System.Text.Encoding.GetEncoding(28591).GetBytes(text));

            PatchSession session = fx.Session();
            PatchState want = Both(session);
            want.Rate = 1.5;
            Check.False(session.Apply(want).Ok, "refused without the override");

            WriteReport overridden = session.Apply(want, true);
            Check.True(overridden.Ok, "the override lets the commit through");
            Check.Equal("4.1667e-08", IniFile.Load(fx.HsieIni).Value("Val9"),
                "APE's value replaced the hand-edit");
        }

        public static void Test_AnApeWrittenHsieIniValueStillVerifiesOnASecondCommit()
        {
            // The regression this check most risks: APE's own previous write
            // must never be mistaken for a foreign one on the next commit.
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession first = fx.Session();
            PatchState want = Both(first);
            want.Rate = 2.5;
            Check.True(first.Apply(want).Ok, "first commit lands");

            PatchSession second = fx.Session();
            PatchState again = second.Current.Clone();
            again.Rate = 5.0;
            WriteReport report = second.Apply(again);
            Check.True(report.Ok, "a second commit over APE's own hsie.ini value still verifies");
            Check.Close(5.0, fx.Session().Current.Rate, 0.1, "and lands the new rate");
        }

        public static void Test_HsiesOwnOriginalHsieIniValueStillVerifies()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));
            Check.True(report.Ok, "h.sie's stock hsie.ini lines verify cleanly");
        }

        // -- 10. applied-state detection (ticket 01 stories 10-11) --------------

        public static void Test_AForeignEditToHsieIniDoesNotReadAsApplied()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string text = fx.IniText().Replace("Val9=2e-4", "Val9=9.9e-4");
            File.WriteAllBytes(fx.HsieIni, System.Text.Encoding.GetEncoding(28591).GetBytes(text));

            PatchSession session = fx.Session();
            Check.False(session.Install.Applied(Manifest.DieselPatchId),
                "a value neither APE nor h.sie wrote must not read as an APE patch");
        }

        public static void Test_AForeignEditToAnSh3ExeSitesContextDoesNotReadAsApplied()
        {
            // The sh3.exe counterpart to the hsie.ini case above: a site whose
            // invariant context has moved is someone else's edit, not APE's, even
            // though its field now differs from h.sie's original too. Hull repair
            // has exactly two sites and no hsie.ini sites, so corrupting both
            // contexts leaves nothing else that could still read as applied.
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            PatchState want = session.Current.Clone();
            want.HullApplied = true;
            want.HullMin = 30;
            want.HullMax = 50;
            Check.True(session.Apply(want).Ok, "apply so both fields really differ");

            Site width = Find(fx.Manifest, Manifest.HullPatchId, "hull-roll-width");
            Site floor = Find(fx.Manifest, Manifest.HullPatchId, "hull-roll-floor");
            byte[] exe = fx.ExeBytes();
            exe[width.OffsetFor(Fixtures.TestVersionId)] = 0x90;   // corrupt only the context byte
            exe[floor.OffsetFor(Fixtures.TestVersionId)] = 0x00;
            File.WriteAllBytes(fx.Sh3Exe, exe);

            PatchSession reopened = fx.Session();
            Check.False(reopened.Install.Applied(Manifest.HullPatchId),
                "a site whose context another edit moved must not read as an APE patch");
        }

        public static void Test_AnUnreadableInstallReportsUnreadableAndNoPatchReadsAsApplied()
        {
            string dir = Fixtures.TempDir();
            PatchSession session = PatchSession.Open(dir, Manifest.Embedded());

            Check.False(session.Install.Supported, "an empty directory cannot be identified");
            Check.Contains("sh3.exe", session.Install.RefusalReason, "unreadable, not silently patched");
            Check.False(session.Install.Applied(Manifest.HullPatchId), "no patch reads as applied");
            Check.False(session.Install.Applied(Manifest.DieselPatchId), "for either feature");
        }

        // -- 10. the diesel damage report, a third site type (tickets 09-10) ---

        public static void Test_ApplyingDieselDamageRewordsTheReportAndDropsTheGate()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            PatchSession session = fx.Session();
            Check.True(session.Apply(Both(session)).Ok, "apply");

            IniFile menu = IniFile.Load(fx.MenuText);
            Check.Equal("4893=Diesel trouble, Kaleun - normal wear and tear.",
                menu.Line("4893"), "the report's new wording");

            Site gate = Find(fx.Manifest, Manifest.DieselPatchId, "diesel-message-gate");
            Check.Bytes(Hex.ToBytes("9090"),
                Slice(fx.ExeBytes(), gate.OffsetFor(Fixtures.TestVersionId), 2),
                "the partial-damage-only je is nopped out, so the report fires unconditionally");
        }

        public static void Test_TheMessageSiteSurvivesAnUnrelatedEdit()
        {
            // The line before and after 4893 in the fixture must not move or
            // change -- APE's round trip touches exactly one line.
            Fixtures.Install fx = Fixtures.Stock(true);
            string before = fx.MenuTextContent();

            PatchSession session = fx.Session();
            Check.True(session.Apply(Both(session)).Ok, "apply");
            Check.Contains("4892=Diesel engine damaged!", fx.MenuTextContent(),
                "the neighbouring line is untouched");
            Check.Contains("4894=Diesel engine repaired.", fx.MenuTextContent(),
                "on both sides");
            Check.Equal(CountNewlines(before), CountNewlines(fx.MenuTextContent()),
                "no line was added or dropped");
        }

        public static void Test_UncheckingDieselDamageRestoresTheReportAndTheGateByteForByte()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            byte[] exeBefore = fx.ExeBytes();
            string menuBefore = fx.MenuTextContent();

            PatchSession session = fx.Session();
            Check.True(session.Apply(Both(session)).Ok, "apply");
            Check.False(Same(exeBefore, fx.ExeBytes()), "sh3.exe really changed");
            Check.False(menuBefore == fx.MenuTextContent(), "en_menu.txt really changed");

            PatchSession applied = fx.Session();
            PatchState off = applied.Current.Clone();
            off.HullApplied = false;
            off.DieselApplied = false;
            Check.True(applied.Apply(off).Ok, "unchecking diesel damage");

            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is byte-identical to the start");
            Check.Equal(menuBefore, fx.MenuTextContent(), "en_menu.txt is byte-identical too");
        }

        public static void Test_AHandEditedMenuTextLineIsRefusedTheSameWayHsieIniIs()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            string text = fx.MenuTextContent().Replace(
                "If we don't cut power now, we're risking a breakdown!",
                "Something a player wrote by hand.");
            File.WriteAllBytes(fx.MenuText, System.Text.Encoding.GetEncoding(28591).GetBytes(text));

            byte[] exeBefore = fx.ExeBytes();
            string menuBefore = fx.MenuTextContent();

            PatchSession session = fx.Session();
            WriteReport report = session.Apply(Both(session));

            Check.False(report.Ok, "the commit is refused");
            Check.True(report.ForeignWrite, "and flagged as the overridable foreign-write refusal");
            Check.Contains("4893", report.Message, "the refusal names the key");
            Check.Bytes(exeBefore, fx.ExeBytes(), "sh3.exe is untouched");
            Check.Equal(menuBefore, fx.MenuTextContent(), "en_menu.txt is untouched, hand-edit and all");

            WriteReport overridden = session.Apply(Both(session), true);
            Check.True(overridden.Ok, "applying again with allowForeignWrite proceeds");
        }

        public static void Test_AMissingMenuTextFileIsReportedNotCrashed()
        {
            Fixtures.Install fx = Fixtures.Stock(true);
            File.Delete(fx.MenuText);

            PatchSession session = fx.Session();
            Check.False(session.Install.Supported, "no en_menu.txt is not a readable install");
            Check.Contains("en_menu.txt", session.Install.RefusalReason, "and says what is missing");
        }

        // -- 11. manifest-authored bounds (ticket 06) ---------------------------

        public static void Test_HullSitesCarryTheManifestsAuthoredBoundsNotAHardcodedLiteral()
        {
            // The window used to hard-code 0-100 for both hull spinners
            // regardless of which field they bound. Now it reads min/max off the
            // manifest, so the fix has to actually be there to read.
            Manifest m = Manifest.Embedded();
            Site floor = Find(m, Manifest.HullPatchId, "hull-roll-floor");
            Site width = Find(m, Manifest.HullPatchId, "hull-roll-width");

            Check.True(floor.HasRange, "the floor site carries an authored range");
            Check.Close(0.0, floor.Min, 1e-9, "floor's minimum");
            Check.Close(100.0, floor.Max, 1e-9, "floor's maximum");

            Check.True(width.HasRange, "the width site carries an authored range");
            Check.Close(1.0, width.Min, 1e-9, "width's minimum differs from floor's");
            Check.Close(101.0, width.Max, 1e-9, "width's maximum differs from floor's");
        }

        // -- helpers -----------------------------------------------------------

        static string IniLine(Fixtures.Install fx, string key)
        {
            return IniFile.Load(fx.HsieIni).Line(key);
        }

        static Site Find(Manifest m, string patchId, string siteId)
        {
            foreach (Site s in m.Patch(patchId).Sites)
                if (s.Id == siteId) return s;
            throw new Exception("no site " + siteId);
        }

        static byte[] Slice(byte[] b, int at, int length)
        {
            byte[] outp = new byte[length];
            Array.Copy(b, at, outp, 0, length);
            return outp;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static int CountNewlines(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }
    }
}
