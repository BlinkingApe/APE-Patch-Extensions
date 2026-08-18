// The one seam.
//
// PatchSession is the only thing in APE that touches a file.  Open it against a
// game directory and it reports what that install is and what it currently
// holds; hand it a desired state and it verifies every site, then writes all of
// it or none of it.
//
// The WinForms layer above binds controls to the PatchState this hands out,
// calls Apply, and renders the reports.  No probability, no offset and no
// encoding rule lives up there.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace ApePatcher
{
    public class InstallReport
    {
        public string InstallPath;
        public bool Supported;
        public string RefusalReason;
        public string SupportedBuilds;

        public string BuildName = "unknown";
        public string HsieVersion = "unknown";
        public string Sha256 = "";
        public string VersionId;

        /// <summary>h.sie's own Diesel Damage switch, flag-table entry #20. APE
        /// reads this table and never writes it (ticket 05, decision A-2).</summary>
        public bool DieselFlagOn;

        readonly Dictionary<string, bool> available = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly Dictionary<string, bool> applied = new Dictionary<string, bool>(StringComparer.Ordinal);

        public void SetAvailable(string patchId, bool value) { available[patchId] = value; }
        public void SetApplied(string patchId, bool value) { applied[patchId] = value; }

        /// <summary>False when h.sie's own switch for this feature is off, so the
        /// code APE would write into would never run.</summary>
        public bool Available(string patchId)
        {
            bool v;
            return available.TryGetValue(patchId, out v) && v;
        }

        /// <summary>True when this feature's sites hold APE's values rather than
        /// h.sie's originals.</summary>
        public bool Applied(string patchId)
        {
            bool v;
            return applied.TryGetValue(patchId, out v) && v;
        }

        public string ShortSha
        {
            get { return Sha256.Length >= 8 ? Sha256.Substring(0, 8) : Sha256; }
        }
    }

    public class WriteReport
    {
        public bool Ok;
        public string Message = "";
        public List<string> SitesWritten = new List<string>();

        /// <summary>True when the refusal is specifically that a hsie.ini site
        /// holds a value APE did not write -- the one refusal a player can
        /// deliberately proceed past (ticket 01 story 9), by calling Apply again
        /// with allowForeignWrite: true.</summary>
        public bool ForeignWrite;
    }

    /// <summary>One site whose target bytes differ from what is on disk. The unit
    /// both the review list and the commit are counted in.</summary>
    public class PlannedWrite
    {
        public Patch Patch;
        public Site Site;
    }

    public class WritePlan
    {
        public List<PlannedWrite> Writes = new List<PlannedWrite>();

        public bool Touches(string patchId)
        {
            foreach (PlannedWrite w in Writes)
                if (w.Patch.Id == patchId) return true;
            return false;
        }

        public int FeatureCount
        {
            get
            {
                List<string> seen = new List<string>();
                foreach (PlannedWrite w in Writes)
                    if (!seen.Contains(w.Patch.Id)) seen.Add(w.Patch.Id);
                return seen.Count;
            }
        }

        public bool Empty { get { return Writes.Count == 0; } }
    }

    public class PatchSession
    {
        public const string Sh3ExeName = "sh3.exe";
        public const string HsieIniName = "hsie.ini";

        readonly Manifest manifest;
        readonly string dir;
        byte[] exe;          // null when the install could not be read
        IniFile ini;

        /// <summary>The file-write boundary, seamed so tests can inject a write
        /// failure -- including a rollback's own write -- without a real
        /// read-only disk (ticket 01 story 14). Defaults to the real thing.</summary>
        internal Action<string, byte[]> WriteExeBytes = File.WriteAllBytes;
        internal Action<IniFile, string> SaveIni = delegate(IniFile f, string p) { f.Save(p); };

        public InstallReport Install { get; private set; }

        /// <summary>What the install currently holds, as a desired-state model the
        /// UI can bind to and hand straight back.</summary>
        public PatchState Current { get; private set; }

        public Manifest Manifest { get { return manifest; } }

        PatchSession(string gameDir, Manifest m)
        {
            dir = gameDir;
            manifest = m;
        }

        public static PatchSession Open(string gameDir)
        {
            return Open(gameDir, ApePatcher.Manifest.Embedded());
        }

        public static PatchSession Open(string gameDir, Manifest m)
        {
            PatchSession s = new PatchSession(gameDir, m);
            s.Read();
            return s;
        }

        /// <summary>The directory APE writes to: its own, because it ships as one
        /// exe dropped in the game root.</summary>
        public static string OwnDirectory()
        {
            return Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        string ExePath { get { return Path.Combine(dir, Sh3ExeName); } }
        string IniPath { get { return Path.Combine(dir, HsieIniName); } }

        // -- opening -----------------------------------------------------------

        void Read()
        {
            Install = new InstallReport();
            Install.InstallPath = dir;
            Install.SupportedBuilds = manifest.SupportedBuildsSentence();
            Current = new PatchState();

            if (!File.Exists(ExePath))
            {
                Install.RefusalReason = "No " + Sh3ExeName + " here. Put APE in your SH3 "
                    + "game folder, beside " + Sh3ExeName + ".";
                return;
            }
            if (!File.Exists(IniPath))
            {
                Install.RefusalReason = "No " + HsieIniName + " here. APE needs h.sie's "
                    + "hardcode fixes installed.";
                return;
            }

            try
            {
                exe = File.ReadAllBytes(ExePath);
                ini = IniFile.Load(IniPath);
            }
            catch (Exception e)
            {
                exe = null;
                Install.RefusalReason = "APE could not read this install: " + e.Message;
                return;
            }

            Install.Sha256 = Sha256Of(exe);
            SupportedVersion version = manifest.Identify(exe);
            Install.HsieVersion = ReadHsieVersion();

            if (version == null)
            {
                Install.BuildName = "unrecognised build";
                Install.RefusalReason = "APE does not recognise this " + Sh3ExeName
                    + " and will not write to it. Supported: " + Install.SupportedBuilds + ".";
                return;
            }

            Install.Supported = true;
            Install.VersionId = version.Id;
            Install.BuildName = version.BuildName;
            Install.DieselFlagOn = ReadFlag(manifest.Patch(Manifest.DieselPatchId).FlagIndex);

            foreach (Patch p in manifest.Patches)
            {
                Install.SetAvailable(p.Id, p.FlagIndex == 0 || ReadFlag(p.FlagIndex));
                Install.SetApplied(p.Id, IsApplied(p));
            }

            ReadBack();
        }

        string ReadHsieVersion()
        {
            int at = manifest.VersionStringOffset;
            if (exe == null || at + manifest.VersionStringLength > exe.Length) return "unknown";
            return System.Text.Encoding.ASCII
                .GetString(exe, at, manifest.VersionStringLength).Trim();
        }

        bool ReadFlag(int oneBasedIndex)
        {
            if (oneBasedIndex <= 0 || oneBasedIndex > manifest.FlagCount) return false;
            int at = manifest.FlagTableOffset + oneBasedIndex - 1;
            return at < exe.Length && exe[at] == 0xFF;
        }

        /// <summary>Does this feature hold what APE would write, as opposed to
        /// merely differing from h.sie's original? A site another mod touched
        /// differs from the original too, but it is not APE's, so it must not
        /// read as applied (ticket 01 story 10). For sh3.exe that means the same
        /// test Verify uses to trust a value APE itself wrote: the invariant
        /// context bytes still match what APE was proven against, with only the
        /// mutable field differing -- a site whose context has moved is someone
        /// else's edit, not APE's, whatever its field holds. A site that cannot
        /// be read at all is skipped rather than counted either way (story 11) --
        /// in practice this only reaches a hsie.ini key missing from the file,
        /// since a supported build's sh3.exe offsets are proven in range by
        /// Manifest.Identify before this ever runs.</summary>
        bool IsApplied(Patch p)
        {
            bool foundApeValue = false;
            foreach (Site s in p.Sites)
            {
                if (s.Store == SiteStore.Sh3Exe)
                {
                    byte[] onDisk = ReadRun(s);
                    if (onDisk == null) continue;
                    if (!ContextMatches(s, onDisk)) continue;
                    if (!Same(onDisk, s.Original)) foundApeValue = true;
                }
                else
                {
                    string line = ini.Line(s.Key);
                    if (line == null) continue;
                    if (string.Equals(line, s.OriginalLine, StringComparison.Ordinal)) continue;
                    if (IniLineIsRecognised(s, line)) foundApeValue = true;
                }
            }
            return foundApeValue;
        }

        /// <summary>True when a run's invariant context bytes match what APE was
        /// proven against -- the same check Verify performs before writing,
        /// reused here so "holds what APE would write" means the same thing in
        /// both places.</summary>
        static bool ContextMatches(Site s, byte[] run)
        {
            foreach (KeyValuePair<int, byte[]> ctx in s.Context)
                for (int i = 0; i < ctx.Value.Length; i++)
                    if (run[ctx.Key + i] != ctx.Value[i]) return false;
            return true;
        }

        /// <summary>True when a hsie.ini line is either h.sie's original or
        /// something APE itself could have written -- its fixed literal for a
        /// literal site, or a number that round-trips through APE's own
        /// scientific-notation formatting for a derived one. Anything else is a
        /// player's hand-edit or a foreign tool, which APE must not silently
        /// treat as its own (tickets 01 stories 7, 10, 12).</summary>
        static bool IniLineIsRecognised(Site s, string line)
        {
            if (string.Equals(line, s.OriginalLine, StringComparison.Ordinal)) return true;
            if (s.Source == Source.Literal)
                return string.Equals(line, s.Key + "=" + s.LiteralValue, StringComparison.Ordinal);

            int eq = line.IndexOf('=');
            if (eq < 0) return false;
            string raw = line.Substring(eq + 1).Trim(' ', '\t');
            double value;
            if (!Fmt.TryParse(raw, out value)) return false;
            return string.Equals(raw, Fmt.Sci(value), StringComparison.Ordinal);
        }

        void ReadBack()
        {
            Current.HullApplied = Install.Applied(Manifest.HullPatchId);
            Current.DieselApplied = Install.Applied(Manifest.DieselPatchId)
                                    && Install.Available(Manifest.DieselPatchId);

            // Hull repair only reads back once APE has applied it: APE's shipped
            // default (0-5) no longer equals h.sie's own stock roll (5-20), so an
            // unapplied install has nothing of APE's to show. The panel opens on
            // APE's default instead, the same pattern diesel damage already uses
            // below.
            if (Install.Applied(Manifest.HullPatchId))
            {
                double floor, width;
                if (TryFieldValue(Manifest.HullPatchId, Source.HullFloor, out floor)
                    && TryFieldValue(Manifest.HullPatchId, Source.HullWidth, out width)
                    && width >= 1)
                {
                    Current.HullMin = (int)floor;
                    Current.HullMax = (int)(floor + width - 1);
                }
            }

            // Diesel damage only reads back once APE has applied it: h.sie ships
            // two different bands selected on damage state, and APE's model has
            // one, so an unapplied install has no single band to show. The panel
            // opens on APE's defaults instead, and the review list says what would
            // change.
            if (Install.Applied(Manifest.DieselPatchId))
            {
                double lo, hi;
                if (TryFieldValue(Manifest.DieselPatchId, Source.BandLo, out lo))
                    Current.BandLo = Math.Round(lo, 2);
                if (TryFieldValue(Manifest.DieselPatchId, Source.BandHi, out hi))
                    Current.BandHi = Math.Round(hi, 2);

                Site rate = SiteWithSource(manifest.Patch(Manifest.DieselPatchId), Source.Pdso);
                double pdso;
                if (rate != null && ini.TryNumber(rate.Key, out pdso) && pdso > 0)
                {
                    Current.SetRateFromPdso(pdso);
                    Current.Rate = Math.Round(Current.Rate, 1);
                }
            }
        }

        bool TryFieldValue(string patchId, Source source, out double value)
        {
            value = 0.0;
            Site s = SiteWithSource(manifest.Patch(patchId), source);
            if (s == null || !s.HasField) return false;
            byte[] run = ReadRun(s);
            if (run == null) return false;
            value = DecodeField(s, run);
            return true;
        }

        static Site SiteWithSource(Patch p, Source source)
        {
            foreach (Site s in p.Sites)
                if (s.Source == source && s.Store == SiteStore.Sh3Exe && s.HasField) return s;
            foreach (Site s in p.Sites)
                if (s.Source == source) return s;
            return null;
        }

        byte[] ReadRun(Site s)
        {
            int at = s.OffsetFor(Install.VersionId ?? Fallback(s));
            if (at < 0 || at + s.Original.Length > exe.Length) return null;
            byte[] run = new byte[s.Original.Length];
            Array.Copy(exe, at, run, 0, run.Length);
            return run;
        }

        string Fallback(Site s)
        {
            foreach (KeyValuePair<string, int> kv in s.Offsets) return kv.Key;
            throw new PatchException("Site " + s.Id + " has no offsets at all.");
        }

        // -- planning ----------------------------------------------------------

        /// <summary>Every site whose target bytes or line differ from what is on
        /// disk. Both the review list and the commit are counted in these, so
        /// "one feature will change" and "one feature was written" are the same
        /// claim.</summary>
        public WritePlan Plan(PatchState desired)
        {
            WritePlan plan = new WritePlan();
            if (!Install.Supported) return plan;

            foreach (Patch p in manifest.Patches)
            {
                // A feature h.sie's own switch has turned off is left entirely
                // alone: APE never writes into code that would not run.
                if (!Install.Available(p.Id)) continue;
                if (!Wanted(p, desired) && !Install.Applied(p.Id)) continue;

                foreach (Site s in p.Sites)
                {
                    if (s.Store == SiteStore.Sh3Exe)
                    {
                        byte[] want = TargetRun(p, s, desired);
                        byte[] have = ReadRun(s);
                        if (have == null || !Same(have, want)) Add(plan, p, s);
                    }
                    else
                    {
                        string want = TargetLine(p, s, desired);
                        string have = ini.Line(s.Key);
                        if (have == null || !string.Equals(have, want, StringComparison.Ordinal))
                            Add(plan, p, s);
                    }
                }
            }
            return plan;
        }

        static void Add(WritePlan plan, Patch p, Site s)
        {
            PlannedWrite w = new PlannedWrite();
            w.Patch = p;
            w.Site = s;
            plan.Writes.Add(w);
        }

        static bool Wanted(Patch p, PatchState desired)
        {
            return p.Id == Manifest.HullPatchId ? desired.HullApplied : desired.DieselApplied;
        }

        byte[] TargetRun(Patch p, Site s, PatchState desired)
        {
            if (!Wanted(p, desired)) return s.Original;
            if (s.Encoding == FieldEncoding.FixedRewrite) return s.Replacement;

            byte[] run = (byte[])s.Original.Clone();
            byte[] field = EncodeField(s, ValueFor(s.Source, desired));
            Array.Copy(field, 0, run, s.FieldAt, field.Length);
            return run;
        }

        string TargetLine(Patch p, Site s, PatchState desired)
        {
            if (!Wanted(p, desired)) return s.OriginalLine;
            return s.Key + "=" + IniValueFor(s, desired);
        }

        static double ValueFor(Source source, PatchState d)
        {
            switch (source)
            {
                case Source.HullWidth: return d.HullRollWidth;
                case Source.HullFloor: return d.HullRollFloor;
                case Source.BandLo: return d.BandLo;
                case Source.BandHi: return d.BandHi;
                case Source.Pdso: return d.Pdso;
                case Source.Threshold: return d.Threshold;
            }
            throw new PatchException("No value source for a site that needs one.");
        }

        static string IniValueFor(Site s, PatchState d)
        {
            if (s.Source == Source.Literal) return s.LiteralValue;
            // Scientific notation, invariant culture: h.sie's parser expects
            // "4.1667e-08", and a comma-decimal machine writing "4,1667e-08"
            // would render the mod inert with no error and no in-game symptom.
            return Fmt.Sci(ValueFor(s.Source, d));
        }

        static byte[] EncodeField(Site s, double value)
        {
            switch (s.FieldType)
            {
                case "u8":
                    return new byte[] { (byte)Clamp(value, 0, 255) };
                case "u32":
                    return BitConverter.GetBytes((uint)Clamp(value, 0, uint.MaxValue));
                case "f32":
                    return BitConverter.GetBytes((float)value);
            }
            throw new PatchException("Site " + s.Id + " has an unknown field type '"
                + s.FieldType + "'.");
        }

        static double DecodeField(Site s, byte[] run)
        {
            switch (s.FieldType)
            {
                case "u8": return run[s.FieldAt];
                case "u32": return BitConverter.ToUInt32(run, s.FieldAt);
                case "f32": return BitConverter.ToSingle(run, s.FieldAt);
            }
            throw new PatchException("Site " + s.Id + " has an unknown field type '"
                + s.FieldType + "'.");
        }

        static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        // -- committing --------------------------------------------------------

        public WriteReport Apply(PatchState desired)
        {
            return Apply(desired, false);
        }

        /// <summary>allowForeignWrite lets a player proceed past a refusal that a
        /// hsie.ini site holds a value APE did not write -- their own hand-edit,
        /// deliberately overwritten with APE's value (ticket 01 story 9). It has
        /// no effect on the sh3.exe context check, which stays absolute: that
        /// refusal means APE cannot prove what it would be writing over.</summary>
        public WriteReport Apply(PatchState desired, bool allowForeignWrite)
        {
            WriteReport report = new WriteReport();

            if (!Install.Supported)
            {
                report.Message = Install.RefusalReason
                    ?? "APE does not recognise this install and will not write to it.";
                return report;
            }
            if (GameIsRunning())
            {
                report.Message = "SH3 is running. Close the game before applying, so APE is "
                    + "not writing to a binary Windows currently has loaded.";
                return report;
            }

            // The UI disables Apply on an invalid model, but PatchSession is the
            // seam that actually writes -- it must not trust a caller above it to
            // have checked first (ticket 01 story 3).
            if (desired.DieselApplied && !desired.RateValid)
            {
                report.Message = "APE refused to write: the rate must be above 0 and at or below "
                    + Fmt.Int((int)PatchState.RateCeiling) + " events per 10,000 hours.";
                return report;
            }
            if (desired.DieselApplied && !desired.BandValid)
            {
                report.Message = "APE refused to write: the damage band needs a low end above 0 "
                    + "and below the high end, which must itself be below 1.";
                return report;
            }
            if (desired.HullApplied && !desired.HullValid)
            {
                report.Message = "APE refused to write: the hull repair minimum must not be above "
                    + "the maximum.";
                return report;
            }

            // Verify every site of every feature APE is responsible for, before a
            // single byte is written. One mismatch aborts the whole commit --
            // including the other store's -- so nothing is ever half-patched.
            bool foreignWrite;
            string failure = Verify(allowForeignWrite, out foreignWrite);
            if (failure != null)
            {
                report.Message = failure;
                report.ForeignWrite = foreignWrite;
                return report;
            }

            WritePlan plan = Plan(desired);
            if (plan.Empty)
            {
                report.Ok = true;
                report.Message = "Nothing to apply.";
                return report;
            }

            byte[] newExe = (byte[])exe.Clone();
            IniFile newIni = IniFile.Load(IniPath);
            bool exeChanged = false, iniChanged = false;

            foreach (PlannedWrite w in plan.Writes)
            {
                if (w.Site.Store == SiteStore.Sh3Exe)
                {
                    byte[] run = TargetRun(w.Patch, w.Site, desired);
                    Array.Copy(run, 0, newExe, w.Site.OffsetFor(Install.VersionId), run.Length);
                    exeChanged = true;
                }
                else
                {
                    newIni.SetLine(w.Site.Key, TargetLine(w.Patch, w.Site, desired));
                    iniChanged = true;
                }
                report.SitesWritten.Add(w.Site.Describe());
            }

            byte[] exeBackup = exe;
            try
            {
                if (exeChanged) WriteExeBytes(ExePath, newExe);
            }
            catch (Exception e)
            {
                report.SitesWritten.Clear();
                report.Message = CannotWrite(Sh3ExeName, e);
                return report;
            }
            try
            {
                if (iniChanged) SaveIni(newIni, IniPath);
            }
            catch (Exception iniFailure)
            {
                // Put sh3.exe back rather than leave the install half-patched.
                // A rollback that itself throws is not swallowed: the player is
                // told exactly which files are in which state and how to recover
                // (ticket 01 stories 5-6), instead of seeing only the ini error
                // while sh3.exe sits silently patched.
                report.SitesWritten.Clear();
                if (exeChanged)
                {
                    try
                    {
                        WriteExeBytes(ExePath, exeBackup);
                    }
                    catch (Exception rollbackFailure)
                    {
                        report.Message = InconsistentState(iniFailure, rollbackFailure);
                        return report;
                    }
                }
                report.Message = CannotWrite(HsieIniName, iniFailure);
                return report;
            }

            exe = newExe;
            ini = newIni;
            report.Ok = true;
            report.Message = Describe(plan);

            // The window has to keep describing the install, so re-read it.
            Read();
            return report;
        }

        static string CannotWrite(string file, Exception e)
        {
            return "APE could not write " + file + ": " + e.Message
                + Environment.NewLine
                + "If SH3 is installed under Program Files, run APE as administrator, "
                + "or move the game somewhere you can write to.";
        }

        /// <summary>The one outcome the header comment used to promise could not
        /// happen: sh3.exe was written, hsie.ini was not, and putting sh3.exe back
        /// failed too. Names both files' actual states and what to do about it,
        /// rather than discarding the rollback's own failure (ticket 01 stories
        /// 5-6, 14).</summary>
        static string InconsistentState(Exception iniFailure, Exception rollbackFailure)
        {
            return "APE could not write " + HsieIniName + " (" + iniFailure.Message + "), and then "
                + "could not restore " + Sh3ExeName + " to what it was before this attempt ("
                + rollbackFailure.Message + ")."
                + Environment.NewLine + Environment.NewLine
                + "Your install is now inconsistent: " + Sh3ExeName + " holds the change APE just "
                + "made, but " + HsieIniName + " does not."
                + Environment.NewLine + Environment.NewLine
                + "To recover: close APE, make sure " + HsieIniName + " is writable (not read-only, "
                + "not open in another program, not synced by a cloud backup tool), then run APE "
                + "again and apply the same change -- that finishes writing " + HsieIniName + " so "
                + "it agrees with " + Sh3ExeName + ". If it fails again, restore both files from a "
                + "backup or reinstall h.sie's hardcode fixes before trying once more.";
        }

        static string Describe(WritePlan plan)
        {
            Dictionary<string, int> byPatch = new Dictionary<string, int>(StringComparer.Ordinal);
            List<string> order = new List<string>();
            foreach (PlannedWrite w in plan.Writes)
            {
                if (!byPatch.ContainsKey(w.Patch.Name)) { byPatch[w.Patch.Name] = 0; order.Add(w.Patch.Name); }
                byPatch[w.Patch.Name]++;
            }
            List<string> parts = new List<string>();
            foreach (string name in order)
                parts.Add(name + " (" + Fmt.Int(byPatch[name])
                    + (byPatch[name] == 1 ? " site)" : " sites)"));
            return "Written: " + string.Join(", ", parts.ToArray()) + ".";
        }

        /// <summary>Check every site's invariant context bytes for sh3.exe, never
        /// its mutable field -- that is what lets a value APE itself wrote
        /// coexist with verify-before-write. For hsie.ini, check the whole line
        /// against both h.sie's original and APE's own recognisable formats,
        /// which makes the same promise the context-byte check makes: a value
        /// neither APE nor h.sie put there is not silently overwritten (ticket 01
        /// stories 7, 12). Returns null when everything checks out, or a message
        /// naming the first offending site and what was found there; foreignWrite
        /// is true only for the one refusal a caller can proceed past.</summary>
        string Verify(bool allowForeignWrite, out bool foreignWrite)
        {
            foreignWrite = false;
            foreach (Patch p in manifest.Patches)
            {
                if (!Install.Available(p.Id)) continue;
                foreach (Site s in p.Sites)
                {
                    if (s.Store == SiteStore.Sh3Exe)
                    {
                        byte[] run = ReadRun(s);
                        if (run == null)
                            return "APE refused to write: " + s.Describe()
                                + " falls outside this " + Sh3ExeName + ".";
                        foreach (KeyValuePair<int, byte[]> ctx in s.Context)
                        {
                            for (int i = 0; i < ctx.Value.Length; i++)
                            {
                                if (run[ctx.Key + i] == ctx.Value[i]) continue;
                                byte[] found = new byte[ctx.Value.Length];
                                Array.Copy(run, ctx.Key, found, 0, found.Length);
                                return "APE refused to write. " + s.Describe()
                                    + " does not hold the bytes APE was proven against:"
                                    + Environment.NewLine
                                    + "  expected " + Hex.ToString(ctx.Value)
                                    + ", found " + Hex.ToString(found) + "."
                                    + Environment.NewLine
                                    + "Nothing was written. Restore an unmodified "
                                    + Sh3ExeName + " and try again.";
                            }
                        }
                    }
                    else
                    {
                        string have = ini.Line(s.Key);
                        if (have == null)
                            return "APE refused to write. " + s.Describe()
                                + " is not in this " + HsieIniName + "."
                                + Environment.NewLine
                                + "Nothing was written, in either file.";
                        if (IniLineIsRecognised(s, have)) continue;
                        if (allowForeignWrite) continue;
                        foreignWrite = true;
                        return "APE refused to write. hsie.ini " + s.Key + " holds a value APE did "
                            + "not write:"
                            + Environment.NewLine
                            + "  found in your file   " + have
                            + Environment.NewLine
                            + "  h.sie's original      " + s.OriginalLine
                            + Environment.NewLine
                            + "Nothing was written, in either file. This is usually your own edit to "
                            + HsieIniName + " -- APE will not overwrite it silently. Keep it as it "
                            + "is, or apply again to proceed and let APE write its own value over it.";
                    }
                }
            }
            return null;
        }

        static bool GameIsRunning()
        {
            try
            {
                return Process.GetProcessesByName("sh3").Length > 0;
            }
            catch
            {
                return false;   // never block a commit because a process query failed
            }
        }

        // -- helpers -----------------------------------------------------------

        static string Sha256Of(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex.ToString(sha.ComputeHash(bytes));
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
