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

        public string HsieVersion = "unknown";
        public string Sha256 = "";

        /// <summary>h.sie's own Diesel Damage switch, flag-table entry #20. APE
        /// reads this table and never writes it (ticket 05, decision A-2).</summary>
        public bool DieselFlagOn;

        /// <summary>Every playable U-boat class found under data/Submarine, for
        /// the crush-time patch (ticket 16). Empty when there are none.</summary>
        public List<UBoatZon> UBoats = new List<UBoatZon>();

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

    /// <summary>One .zon file the crush-time patch will write: APE's crush
    /// speed, or the pristine copy back when Restore is set.</summary>
    public class ZonWrite
    {
        public UBoatZon Boat;
        public bool Restore;
        public byte[] NewBytes;
        public float CrashSpeed;      // what NewBytes holds
    }

    /// <summary>Depth stress's one file: ape.act deployed with the desired
    /// values, or deleted (ADR-0006).</summary>
    public class ApeActWrite
    {
        public bool Delete;
        public byte[] NewBytes;       // null when deleting, or when Refusal is set
        /// <summary>Why the template can't be written (a wrong marker or offset);
        /// Apply refuses the whole commit rather than write anything.</summary>
        public string Refusal;
    }

    public class WritePlan
    {
        public List<PlannedWrite> Writes = new List<PlannedWrite>();
        public List<ZonWrite> ZonWrites = new List<ZonWrite>();
        public ApeActWrite ApeAct;    // null when ape.act stays as it is

        public bool Touches(string patchId)
        {
            if (patchId == CrushTime.PatchId) return ZonWrites.Count > 0;
            if (patchId == DepthStress.PatchId) return ApeAct != null;
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
                return seen.Count + (ZonWrites.Count > 0 ? 1 : 0) + (ApeAct != null ? 1 : 0);
            }
        }

        public bool Empty { get { return Writes.Count == 0 && ZonWrites.Count == 0 && ApeAct == null; } }
    }

    public class PatchSession
    {
        public const string Sh3ExeName = "sh3.exe";
        public const string HsieIniName = "hsie.ini";
        public const string MenuTextName = "data/Menu/en_menu.txt";

        readonly Manifest manifest;
        readonly string dir;
        byte[] exe;          // null when the install could not be read
        IniFile ini;
        IniFile menuText;
        byte[] apeActTemplate;   // null when this build has none
        byte[] apeAct;           // the deployed ape.act; null when there is none

        /// <summary>The file-write boundary, seamed so tests can inject a write
        /// failure -- including a rollback's own write -- without a real
        /// read-only disk (ticket 01 story 14). Defaults to the real thing.</summary>
        internal Action<string, byte[]> WriteExeBytes = File.WriteAllBytes;
        internal Action<IniFile, string> SaveIni = delegate(IniFile f, string p) { f.Save(p); };
        internal Action<string, byte[]> WriteZonBytes = File.WriteAllBytes;
        internal Action<string> DeleteFile = File.Delete;
        internal Action<string, byte[]> WriteApeActBytes = File.WriteAllBytes;

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
            return Open(gameDir, m, DepthStress.EmbeddedTemplate());
        }

        /// <summary>apeActTemplate is the ape.act depth stress deploys (ADR-0006):
        /// the one embedded in this exe, or null if it was built without one.</summary>
        public static PatchSession Open(string gameDir, Manifest m, byte[] apeActTemplate)
        {
            PatchSession s = new PatchSession(gameDir, m);
            s.apeActTemplate = apeActTemplate;
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
        string MenuTextPath { get { return Path.Combine(dir, "data", "Menu", "en_menu.txt"); } }
        string ApeActPath { get { return Path.Combine(dir, DepthStress.FileName); } }

        /// <summary>Which in-memory store a non-sh3.exe site's key/line lives in.</summary>
        IniFile StoreFor(Site s) { return s.Store == SiteStore.HsieIni ? ini : menuText; }

        string FileNameFor(Site s) { return s.Store == SiteStore.HsieIni ? HsieIniName : MenuTextName; }

        // -- opening -----------------------------------------------------------

        void Read()
        {
            Install = new InstallReport();
            Install.InstallPath = dir;
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
            if (!File.Exists(MenuTextPath))
            {
                Install.RefusalReason = "No " + MenuTextName + " here. This does not look "
                    + "like a Silent Hunter III install.";
                return;
            }

            try
            {
                exe = File.ReadAllBytes(ExePath);
                ini = IniFile.Load(IniPath);
                menuText = IniFile.Load(MenuTextPath);
                apeAct = File.Exists(ApeActPath) ? File.ReadAllBytes(ApeActPath) : null;
            }
            catch (Exception e)
            {
                exe = null;
                Install.RefusalReason = "APE could not read this install: " + e.Message;
                return;
            }

            Install.Sha256 = Sha256Of(exe);
            Install.HsieVersion = ReadHsieVersion();
            Install.Supported = true;
            Install.DieselFlagOn = ReadFlag(manifest.Patch(Manifest.DieselPatchId).FlagIndex);

            foreach (Patch p in manifest.Patches)
            {
                Install.SetAvailable(p.Id, p.FlagIndex == 0 || ReadFlag(p.FlagIndex));
                Install.SetApplied(p.Id, IsApplied(p));
            }

            // The crush-time patch's targets are found, not authored (ticket 16).
            // A Submarine folder APE cannot list leaves the feature unavailable
            // rather than refusing the whole install.
            try { Install.UBoats = CrushTime.Discover(dir); }
            catch (Exception) { Install.UBoats = new List<UBoatZon>(); }
            bool anyWritable = false, anyApplied = false;
            foreach (UBoatZon b in Install.UBoats)
            {
                if (b.State == ZonState.Stock || b.State == ZonState.Applied) anyWritable = true;
                if (b.State == ZonState.Applied) anyApplied = true;
            }
            Install.SetAvailable(CrushTime.PatchId, anyWritable);
            Install.SetApplied(CrushTime.PatchId, anyApplied);

            // Depth stress is applied exactly when ape.act is in the game root:
            // APE deploys it only for a runtime feature, and depth stress is the
            // only one (ADR-0006).
            Install.SetAvailable(DepthStress.PatchId, apeActTemplate != null);
            Install.SetApplied(DepthStress.PatchId, apeAct != null);

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
        /// for sh3.exe that means ReadRun's own bounds check found the site's
        /// offset outside this exe, and for hsie.ini a key missing from the
        /// file.</summary>
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
                    string line = StoreFor(s).Line(s.Key);
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
            // Depth stress reads back from the deployed ape.act when there is
            // one, else from the template's defaults (ADR-0006). A struct whose
            // marker doesn't check out is never read.
            Current.DepthApplied = apeAct != null;
            if (!DepthStress.Read(apeAct, manifest.DepthStressStruct, Current))
                DepthStress.Read(apeActTemplate, manifest.DepthStressStruct, Current);

            Current.CrushApplied = Install.Applied(CrushTime.PatchId);
            foreach (UBoatZon b in Install.UBoats)
            {
                int t = b.AppliedCrushTime;
                if (t > 0) { Current.CrushTime = t; break; }
            }

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
            int at = s.Offset;
            if (at < 0 || at + s.Original.Length > exe.Length) return null;
            byte[] run = new byte[s.Original.Length];
            Array.Copy(exe, at, run, 0, run.Length);
            return run;
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
                        string have = StoreFor(s).Line(s.Key);
                        if (have == null || !string.Equals(have, want, StringComparison.Ordinal))
                            Add(plan, p, s);
                    }
                }
            }
            PlanZons(plan, desired);
            PlanApeAct(plan, desired);
            return plan;
        }

        /// <summary>Depth stress's write: the template with the desired values when
        /// wanted, unless the deployed file already holds exactly that; a delete
        /// when not wanted and ape.act is there.</summary>
        void PlanApeAct(WritePlan plan, PatchState desired)
        {
            if (!desired.DepthApplied)
            {
                if (apeAct == null) return;
                plan.ApeAct = new ApeActWrite();
                plan.ApeAct.Delete = true;
                return;
            }
            string problem = Install.Available(DepthStress.PatchId)
                ? DepthStress.Check(apeActTemplate, manifest.DepthStressStruct)
                : "this build of APE carries no " + DepthStress.FileName;
            if (problem != null)
            {
                plan.ApeAct = new ApeActWrite();
                plan.ApeAct.Refusal = problem + ".";
                return;
            }
            byte[] want = DepthStress.Write(apeActTemplate, manifest.DepthStressStruct, desired);
            if (apeAct != null && Same(apeAct, want)) return;
            plan.ApeAct = new ApeActWrite();
            plan.ApeAct.NewBytes = want;
        }

        /// <summary>The crush-time patch's writes. A class another mod has changed,
        /// or one APE cannot read, is never written in either direction. A stock
        /// class is always written when the patch is wanted, even if its value
        /// already matches, because that write is what keeps its pristine copy.</summary>
        void PlanZons(WritePlan plan, PatchState desired)
        {
            foreach (UBoatZon b in Install.UBoats)
            {
                if (b.State != ZonState.Stock && b.State != ZonState.Applied) continue;
                ZonWrite w = new ZonWrite();
                w.Boat = b;
                if (desired.CrushApplied)
                {
                    w.CrashSpeed = Zon.CrashSpeedFor(b.Record.HitPoints, desired.CrushTime);
                    w.NewBytes = Zon.WithCrashSpeed(b.Pristine, b.Record, w.CrashSpeed);
                    if (b.State == ZonState.Applied && Same(b.Bytes, w.NewBytes)) continue;
                }
                else
                {
                    if (b.State != ZonState.Applied) continue;
                    w.Restore = true;
                    w.CrashSpeed = b.Record.CrashSpeed;
                    w.NewBytes = b.Original;
                }
                plan.ZonWrites.Add(w);
            }
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
            if (desired.CrushApplied && !desired.CrushTimeValid)
            {
                report.Message = "APE refused to write: the crush time must be from "
                    + Fmt.Int(PatchState.CrushTimeMin) + " to " + Fmt.Int(PatchState.CrushTimeMax)
                    + " seconds.";
                return report;
            }
            if (desired.HullApplied && !desired.HullValid)
            {
                report.Message = "APE refused to write: the hull repair minimum must not be above "
                    + "the maximum.";
                return report;
            }
            if (desired.DepthApplied && !desired.DepthStressValid)
            {
                report.Message = "APE refused to write: a depth stress value is out of range. "
                    + "Tier-2 depth " + Fmt.Int((int)PatchState.Tier2DepthMin) + "-"
                    + Fmt.Int((int)PatchState.Tier2DepthMax) + " m, leakage "
                    + Fmt.Int((int)PatchState.LeakageMin) + "-" + Fmt.Int((int)PatchState.LeakageMax)
                    + " %/min, wear amount " + Fmt.Int((int)PatchState.WearAmountMin) + "-"
                    + Fmt.Int((int)PatchState.WearAmountMax) + " %, wear rate "
                    + Fmt.Int((int)PatchState.WearRateMin) + "-" + Fmt.Int((int)PatchState.WearRateMax)
                    + " min, hull multiplier " + Fmt.F1(PatchState.HullMultMin) + "-"
                    + Fmt.F1(PatchState.HullMultMax) + ".";
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

            string zonChanged = VerifyZons(plan);
            if (zonChanged != null)
            {
                report.Message = zonChanged;
                return report;
            }
            if (plan.ApeAct != null && plan.ApeAct.Refusal != null)
            {
                report.Message = "APE refused to write: " + plan.ApeAct.Refusal
                    + Environment.NewLine + "Nothing was written, in any file.";
                return report;
            }

            byte[] newExe = (byte[])exe.Clone();
            IniFile newIni = IniFile.Load(IniPath);
            IniFile newMenu = IniFile.Load(MenuTextPath);
            bool exeChanged = false, iniChanged = false, menuChanged = false;

            foreach (PlannedWrite w in plan.Writes)
            {
                if (w.Site.Store == SiteStore.Sh3Exe)
                {
                    byte[] run = TargetRun(w.Patch, w.Site, desired);
                    Array.Copy(run, 0, newExe, w.Site.Offset, run.Length);
                    exeChanged = true;
                }
                else if (w.Site.Store == SiteStore.HsieIni)
                {
                    newIni.SetLine(w.Site.Key, TargetLine(w.Patch, w.Site, desired));
                    iniChanged = true;
                }
                else
                {
                    newMenu.SetLine(w.Site.Key, TargetLine(w.Patch, w.Site, desired));
                    menuChanged = true;
                }
                report.SitesWritten.Add(w.Site.Describe());
            }
            foreach (ZonWrite z in plan.ZonWrites)
                report.SitesWritten.Add(z.Boat.RelativePath + " CrashSpeed");
            if (plan.ApeAct != null)
                report.SitesWritten.Add(DepthStress.FileName
                    + (plan.ApeAct.Delete ? " removed" : " depth-stress constants"));

            byte[] exeBackup = exe;
            IniFile iniBackup = ini;
            IniFile menuBackup = menuText;
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
            try
            {
                if (menuChanged) SaveIni(newMenu, MenuTextPath);
            }
            catch (Exception menuFailure)
            {
                // The third file, added by ticket 10: put both earlier writes
                // back rather than leave the install half-patched, the same
                // guarantee the ini stage above makes for sh3.exe.
                report.SitesWritten.Clear();
                Exception rollbackFailure = null;
                if (iniChanged)
                {
                    try { SaveIni(iniBackup, IniPath); }
                    catch (Exception e) { rollbackFailure = e; }
                }
                if (rollbackFailure == null && exeChanged)
                {
                    try { WriteExeBytes(ExePath, exeBackup); }
                    catch (Exception e) { rollbackFailure = e; }
                }
                if (rollbackFailure != null)
                {
                    report.Message = InconsistentStateAfterMenuFailure(menuFailure, rollbackFailure);
                    return report;
                }
                report.Message = CannotWrite(MenuTextName, menuFailure);
                return report;
            }

            // The .zon files (ticket 16), then ape.act (ticket 22), last. A
            // failure part-way puts back every file this attempt has already
            // written, the earlier stores included.
            Undo undo = new Undo();
            undo.Exe = exeChanged ? exeBackup : null;
            undo.Ini = iniChanged ? iniBackup : null;
            undo.Menu = menuChanged ? menuBackup : null;
            string lateFailure = WriteZons(plan, undo);
            if (lateFailure == null) lateFailure = WriteApeAct(plan, undo);
            if (lateFailure != null)
            {
                report.SitesWritten.Clear();
                report.Message = lateFailure;
                return report;
            }
            FinishZons(plan);

            exe = newExe;
            ini = newIni;
            menuText = newMenu;
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

        /// <summary>The three-file analogue of InconsistentState, for a menu-text
        /// write failure whose rollback of an already-written sh3.exe or hsie.ini
        /// also fails (ticket 10's third site type). Less deeply specced than the
        /// two-file case since it is the tail of a tail, but the same promise
        /// holds: the player is told which files may still hold APE's change.</summary>
        static string InconsistentStateAfterMenuFailure(Exception menuFailure, Exception rollbackFailure)
        {
            return "APE could not write " + MenuTextName + " (" + menuFailure.Message + "), and then "
                + "could not restore an earlier file it had already written for this same change ("
                + rollbackFailure.Message + ")."
                + Environment.NewLine + Environment.NewLine
                + "Your install is now inconsistent: " + Sh3ExeName + " and/or " + HsieIniName
                + " may still hold the change APE just made, but " + MenuTextName + " does not."
                + Environment.NewLine + Environment.NewLine
                + "To recover: close APE, make sure all three files are writable (not read-only, "
                + "not open in another program, not synced by a cloud backup tool), then run APE "
                + "again and apply the same change -- that finishes the write so every file agrees. "
                + "If it fails again, restore all three files from a backup or reinstall h.sie's "
                + "hardcode fixes before trying once more.";
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
            if (plan.ZonWrites.Count > 0)
                parts.Add(CrushTime.PatchName + " (" + Fmt.Int(plan.ZonWrites.Count)
                    + (plan.ZonWrites.Count == 1 ? " U-boat file)" : " U-boat files)"));
            if (plan.ApeAct != null)
                parts.Add(DepthStress.PatchName + " (" + DepthStress.FileName
                    + (plan.ApeAct.Delete ? " removed)" : " deployed)"));
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
                        string fileName = FileNameFor(s);
                        string have = StoreFor(s).Line(s.Key);
                        if (have == null)
                            return "APE refused to write. " + s.Describe()
                                + " is not in this " + fileName + "."
                                + Environment.NewLine
                                + "Nothing was written, in any file.";
                        if (IniLineIsRecognised(s, have)) continue;
                        if (allowForeignWrite) continue;
                        foreignWrite = true;
                        return "APE refused to write. " + fileName + " " + s.Key + " holds a value "
                            + "APE did not write:"
                            + Environment.NewLine
                            + "  found in your file   " + have
                            + Environment.NewLine
                            + "  h.sie's original      " + s.OriginalLine
                            + Environment.NewLine
                            + "Nothing was written, in any file. This is usually your own edit to "
                            + fileName + " -- APE will not overwrite it silently. Keep it as it "
                            + "is, or apply again to proceed and let APE write its own value over it.";
                    }
                }
            }
            return null;
        }

        /// <summary>Every .zon about to be written must still be exactly what this
        /// session read, its pristine copy included: APE judged each file when it
        /// opened, and a file that has changed since is no longer the one it
        /// judged.</summary>
        string VerifyZons(WritePlan plan)
        {
            foreach (ZonWrite w in plan.ZonWrites)
            {
                UBoatZon b = w.Boat;
                bool same;
                try
                {
                    same = File.Exists(b.ZonPath) && Same(File.ReadAllBytes(b.ZonPath), b.Bytes)
                        && (b.Original == null
                            ? !File.Exists(b.OrigPath)
                            : File.Exists(b.OrigPath) && Same(File.ReadAllBytes(b.OrigPath), b.Original));
                }
                catch (Exception)
                {
                    same = false;
                }
                if (!same)
                    return "APE refused to write. " + b.RelativePath + " changed after APE opened "
                        + "this install." + Environment.NewLine
                        + "Nothing was written, in any file. Reopen APE and try again.";
            }
            return null;
        }

        /// <summary>What an apply has written so far, and the earlier files'
        /// contents to put back: everything a failure in a later store must undo.
        /// A null backup means that store wasn't written.</summary>
        class Undo
        {
            public byte[] Exe;
            public IniFile Ini;
            public IniFile Menu;
            public List<UBoatZon> ZonsWritten = new List<UBoatZon>();
            public List<string> CopiesMade = new List<string>();
        }

        /// <summary>Puts back every file `undo` names, newest store first. Returns
        /// the first failure, having still tried every other file; null when all
        /// of it went back.</summary>
        Exception RollBack(Undo undo)
        {
            Exception rollbackFailure = null;
            foreach (UBoatZon b in undo.ZonsWritten)
            {
                try { WriteZonBytes(b.ZonPath, b.Bytes); }
                catch (Exception e) { if (rollbackFailure == null) rollbackFailure = e; }
            }
            foreach (string copy in undo.CopiesMade)
            {
                try { DeleteFile(copy); }
                catch (Exception e) { if (rollbackFailure == null) rollbackFailure = e; }
            }
            if (undo.Menu != null)
            {
                try { SaveIni(undo.Menu, MenuTextPath); }
                catch (Exception e) { if (rollbackFailure == null) rollbackFailure = e; }
            }
            if (undo.Ini != null)
            {
                try { SaveIni(undo.Ini, IniPath); }
                catch (Exception e) { if (rollbackFailure == null) rollbackFailure = e; }
            }
            if (undo.Exe != null)
            {
                try { WriteExeBytes(ExePath, undo.Exe); }
                catch (Exception e) { if (rollbackFailure == null) rollbackFailure = e; }
            }
            return rollbackFailure;
        }

        /// <summary>The message for a failure in a late store (a .zon or ape.act)
        /// whose rollback also failed: which files may still hold the change.</summary>
        static string PartlyRolledBack(string what, Exception failure, Exception rollbackFailure)
        {
            return "APE could not " + what + " (" + failure.Message + "), and then "
                + "could not put back everything it had already written for this change ("
                + rollbackFailure.Message + ")."
                + Environment.NewLine + Environment.NewLine
                + "Your install may now be inconsistent: " + Sh3ExeName + ", " + HsieIniName + ", "
                + MenuTextName + ", a data/Submarine .zon file or " + DepthStress.FileName
                + " may still hold the change APE just made."
                + Environment.NewLine + Environment.NewLine
                + "To recover: close APE, make sure the game folder is writable (not read-only, "
                + "not synced by a cloud backup tool), then run APE again and apply the same "
                + "change, so every file agrees.";
        }

        /// <summary>Writes each planned .zon (its pristine copy first, on APE's
        /// first write), recording each in `undo`. On a failure it puts back
        /// everything this attempt wrote, in every store, and returns the
        /// message; null on success. Pristine copies of reverted classes are
        /// deleted only by FinishZons, once every store has been written, so a
        /// rollback never needs one back.</summary>
        string WriteZons(WritePlan plan, Undo undo)
        {
            try
            {
                foreach (ZonWrite w in plan.ZonWrites)
                {
                    if (!w.Restore && w.Boat.Original == null)
                    {
                        WriteZonBytes(w.Boat.OrigPath, w.Boat.Bytes);
                        undo.CopiesMade.Add(w.Boat.OrigPath);
                    }
                    WriteZonBytes(w.Boat.ZonPath, w.NewBytes);
                    undo.ZonsWritten.Add(w.Boat);
                }
            }
            catch (Exception zonFailure)
            {
                Exception rollbackFailure = RollBack(undo);
                if (rollbackFailure == null)
                    return CannotWrite("a U-boat .zon file", zonFailure);
                return PartlyRolledBack("write a U-boat .zon file", zonFailure, rollbackFailure);
            }
            return null;
        }

        /// <summary>Once every store is written: deletes the pristine copy of
        /// each class reverted.</summary>
        void FinishZons(WritePlan plan)
        {
            foreach (ZonWrite w in plan.ZonWrites)
            {
                if (!w.Restore) continue;
                // The file already holds its original bytes. A copy that will not
                // delete only leaves this class reading as applied at its original
                // crush speed, and the next revert tidies it up.
                try { DeleteFile(w.Boat.OrigPath); }
                catch (Exception) { }
            }
        }

        /// <summary>Deploys or deletes ape.act, the last store written. On a
        /// failure it puts ape.act back as it was, then every earlier store, and
        /// returns the message; null on success.</summary>
        string WriteApeAct(WritePlan plan, Undo undo)
        {
            ApeActWrite w = plan.ApeAct;
            if (w == null) return null;
            try
            {
                if (w.Delete) DeleteFile(ApeActPath);
                else WriteApeActBytes(ApeActPath, w.NewBytes);
            }
            catch (Exception apeFailure)
            {
                // A failed delete leaves the file as it was. A failed write may
                // have left part of a file: put the old one back, or remove it.
                Exception rollbackFailure = null;
                if (!w.Delete)
                {
                    try
                    {
                        // File.Delete is a no-op when nothing was written.
                        if (apeAct != null) WriteApeActBytes(ApeActPath, apeAct);
                        else DeleteFile(ApeActPath);
                    }
                    catch (Exception e) { rollbackFailure = e; }
                }
                Exception rest = RollBack(undo);
                if (rollbackFailure == null) rollbackFailure = rest;

                string what = (w.Delete ? "remove " : "write ") + DepthStress.FileName;
                if (rollbackFailure != null)
                    return PartlyRolledBack(what, apeFailure, rollbackFailure);
                if (!w.Delete) return CannotWrite(DepthStress.FileName, apeFailure);
                return "APE could not remove " + DepthStress.FileName + ": " + apeFailure.Message
                    + Environment.NewLine + "Nothing was changed, in any file. Close SH3 and "
                    + "anything else using " + DepthStress.FileName + ", then try again.";
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
