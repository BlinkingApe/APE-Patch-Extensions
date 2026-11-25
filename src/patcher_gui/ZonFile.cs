// The crush-time patch's store: each playable U-boat class's .zon file
// (depth-pressure-stress ticket 16, ADR-0010).
//
// Unlike every manifest site, a .zon target is found at patch time, not authored:
// which classes are playable, where their records sit and what their original
// CrashSpeed was all differ per class and per bundle (OAE, KC), and ADR-0009
// rules out a per-bundle table.  So:
//
//   * discovery is a rule, not a list -- every data/Submarine/<X>/<X>.cfg whose
//     [Unit] says HumanPlayable=YES (ticket 18, docs/re-notes/07);
//   * the record is found by searching for its key, never by a fixed offset;
//   * the original is a pristine copy, <name>.zon.ape-orig, kept beside the file
//     on APE's first write -- the one narrow exception to "no sidecar state".
//
// CrashSpeed is hull HP per second (the owner's in-game testing), so a crush
// time of t seconds is CrashSpeed = Hit_Points / t.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ApePatcher
{
    /// <summary>Where one class's .zon stands, as far as APE is concerned.</summary>
    public enum ZonState
    {
        /// <summary>No .ape-orig beside it: APE has never written this file.</summary>
        Stock,
        /// <summary>Holds a crush speed APE wrote, and nothing else differs from
        /// APE's pristine copy.</summary>
        Applied,
        /// <summary>APE wrote it once, but another mod has since replaced it.
        /// Left alone, in both directions.</summary>
        ChangedByOtherMod,
        /// <summary>APE cannot find its records safely. Never written.</summary>
        Unreadable
    }

    /// <summary>The two values APE reads from a .zon, and where the one it
    /// writes sits.</summary>
    public class ZonRecord
    {
        public float HitPoints;
        public float CrashSpeed;
        public int CrashSpeedAt;      // file offset of CrashSpeed's float32
    }

    public static class Zon
    {
        public const string Anchor = "ColisionableObject";
        public const string HitPointsKey = "Hit_Points";
        public const string CrashSpeedKey = "CrashSpeed";

        static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        /// <summary>Finds Hit_Points and CrashSpeed. Each must occur exactly once
        /// in the file, after the second ColisionableObject (the first is the
        /// controller's name, the second starts its data), with a record length
        /// of key + NUL + float32 in front of it. Returns null with a reason when
        /// any of that fails.</summary>
        public static ZonRecord Parse(byte[] file, out string problem)
        {
            problem = null;
            List<int> anchors = FindAll(file, Latin1.GetBytes(Anchor + "\0"));
            if (anchors.Count < 2)
            {
                problem = "it has no ColisionableObject block";
                return null;
            }
            int blockAt = anchors[1];

            int hpAt, csAt;
            if (!FindValue(file, HitPointsKey, blockAt, out hpAt, out problem)) return null;
            if (!FindValue(file, CrashSpeedKey, blockAt, out csAt, out problem)) return null;

            ZonRecord r = new ZonRecord();
            r.HitPoints = BitConverter.ToSingle(file, hpAt);
            r.CrashSpeed = BitConverter.ToSingle(file, csAt);
            r.CrashSpeedAt = csAt;
            if (!(r.HitPoints > 0) || float.IsInfinity(r.HitPoints))
            {
                problem = "its Hit_Points is not a positive number";
                return null;
            }
            return r;
        }

        static bool FindValue(byte[] file, string key, int after, out int valueAt, out string problem)
        {
            valueAt = -1;
            problem = null;
            List<int> hits = FindAll(file, Latin1.GetBytes(key + "\0"));
            if (hits.Count != 1)
            {
                problem = key + " occurs " + Fmt.Int(hits.Count) + " times, not once";
                return false;
            }
            int at = hits[0];
            if (at < after)
            {
                problem = key + " is not inside the ColisionableObject block";
                return false;
            }
            if (at < 4 || BitConverter.ToUInt32(file, at - 4) != (uint)(key.Length + 1 + 4))
            {
                problem = key + " does not have the record length APE expects";
                return false;
            }
            valueAt = at + key.Length + 1;
            if (valueAt + 4 > file.Length)
            {
                problem = key + " has no value after it";
                return false;
            }
            return true;
        }

        static List<int> FindAll(byte[] hay, byte[] needle)
        {
            List<int> hits = new List<int>();
            for (int i = 0; i + needle.Length <= hay.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) hits.Add(i);
            }
            return hits;
        }

        /// <summary>A copy of the file with only CrashSpeed's four bytes replaced.</summary>
        public static byte[] WithCrashSpeed(byte[] file, ZonRecord r, float crashSpeed)
        {
            byte[] copy = (byte[])file.Clone();
            BitConverter.GetBytes(crashSpeed).CopyTo(copy, r.CrashSpeedAt);
            return copy;
        }

        /// <summary>CrashSpeed = Hit_Points / crush time, rounded to float32 once,
        /// so a value APE wrote can be recognised exactly on the way back.</summary>
        public static float CrashSpeedFor(float hitPoints, int crushTime)
        {
            return (float)((double)hitPoints / crushTime);
        }

        /// <summary>The whole-second crush time a CrashSpeed was written from, or
        /// 0 when it is not one APE could have written for this hull.</summary>
        public static int CrushTimeOf(float hitPoints, float crashSpeed)
        {
            if (!(crashSpeed > 0) || float.IsInfinity(crashSpeed)) return 0;
            double t = Math.Round(hitPoints / (double)crashSpeed);
            if (t < PatchState.CrushTimeMin || t > PatchState.CrushTimeMax) return 0;
            int whole = (int)t;
            return CrashSpeedFor(hitPoints, whole) == crashSpeed ? whole : 0;
        }
    }

    /// <summary>One playable U-boat class and its .zon, as read when the session
    /// opened.</summary>
    public class UBoatZon
    {
        public string Folder;          // e.g. NSS_Uboat7c
        public string ClassName;       // e.g. SSTypeVIIC, from the unit .cfg
        public string ZonPath;         // absolute
        public string RelativePath;    // data/Submarine/NSS_Uboat7c/NSS_Uboat7c.zon

        public byte[] Bytes;           // the .zon as it is now
        public byte[] Original;        // the .ape-orig copy, or null when there is none
        public ZonRecord Record;       // of Original when APE has one, else of Bytes
        public ZonState State;
        public string Problem;         // why, for ChangedByOtherMod and Unreadable

        public string OrigPath { get { return ZonPath + CrushTime.OrigSuffix; } }
        public string FileName { get { return Path.GetFileName(ZonPath); } }

        /// <summary>The bytes APE would restore: its pristine copy when it has
        /// one, otherwise the file as it stands.</summary>
        public byte[] Pristine { get { return Original ?? Bytes; } }

        /// <summary>The crush time this file currently holds, if APE wrote it.</summary>
        public int AppliedCrushTime
        {
            get
            {
                if (State != ZonState.Applied) return 0;
                return Zon.CrushTimeOf(Record.HitPoints, BitConverter.ToSingle(Bytes, Record.CrashSpeedAt));
            }
        }
    }

    public static class CrushTime
    {
        /// <summary>The patch id the review and the write plan use. Not a manifest
        /// patch: its sites are discovered, not authored.</summary>
        public const string PatchId = "crush-time";
        public const string PatchName = "Crush time";
        public const string OrigSuffix = ".ape-orig";

        static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        /// <summary>Every playable U-boat class under data/Submarine, sorted by
        /// folder. A playable class with no .zon is skipped: there is nothing to
        /// write.</summary>
        public static List<UBoatZon> Discover(string gameDir)
        {
            List<UBoatZon> found = new List<UBoatZon>();
            string root = Path.Combine(Path.Combine(gameDir, "data"), "Submarine");
            if (!Directory.Exists(root)) return found;

            string[] folders = Directory.GetDirectories(root);
            Array.Sort(folders, StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string cfg = FileIn(folder, name + ".cfg");
                string zon = FileIn(folder, name + ".zon");
                if (cfg == null || zon == null) continue;

                string className;
                if (!IsHumanPlayable(File.ReadAllBytes(cfg), out className)) continue;

                UBoatZon boat = new UBoatZon();
                boat.Folder = name;
                boat.ClassName = className;
                boat.ZonPath = zon;
                boat.RelativePath = "data/Submarine/" + name + "/" + Path.GetFileName(zon);
                Classify(boat);
                found.Add(boat);
            }
            return found;
        }

        /// <summary>Case-insensitive, because KC ships NSS_Uboat21.CFG.</summary>
        static string FileIn(string folder, string fileName)
        {
            foreach (string f in Directory.GetFiles(folder))
                if (string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        /// <summary>HumanPlayable=YES in the [Unit] section. A missing key, or any
        /// other value, is not playable.</summary>
        public static bool IsHumanPlayable(byte[] cfg, out string className)
        {
            className = "";
            bool playable = false;
            string section = "";
            foreach (string raw in Latin1.GetString(cfg).Split('\n'))
            {
                string line = raw;
                int semi = line.IndexOf(';');
                if (semi >= 0) line = line.Substring(0, semi);
                line = line.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    int close = line.IndexOf(']');
                    section = close > 0 ? line.Substring(1, close - 1).Trim() : "";
                    continue;
                }
                if (!string.Equals(section, "Unit", StringComparison.OrdinalIgnoreCase)) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (string.Equals(key, "HumanPlayable", StringComparison.OrdinalIgnoreCase))
                    playable = string.Equals(value, "YES", StringComparison.OrdinalIgnoreCase);
                else if (string.Equals(key, "ClassName", StringComparison.OrdinalIgnoreCase))
                    className = value;
            }
            return playable;
        }

        static void Classify(UBoatZon boat)
        {
            string problem;
            try
            {
                boat.Bytes = File.ReadAllBytes(boat.ZonPath);
                if (File.Exists(boat.OrigPath)) boat.Original = File.ReadAllBytes(boat.OrigPath);
            }
            catch (Exception e)
            {
                boat.State = ZonState.Unreadable;
                boat.Problem = e.Message;
                return;
            }

            if (boat.Original == null)
            {
                boat.Record = Zon.Parse(boat.Bytes, out problem);
                boat.State = boat.Record == null ? ZonState.Unreadable : ZonState.Stock;
                boat.Problem = problem;
                return;
            }

            boat.Record = Zon.Parse(boat.Original, out problem);
            if (boat.Record == null)
            {
                boat.State = ZonState.Unreadable;
                boat.Problem = "APE's own copy, " + Path.GetFileName(boat.OrigPath) + ", is damaged: " + problem;
                return;
            }

            // APE's write changes exactly four bytes. Anything else differing from
            // its pristine copy, or a CrashSpeed APE could not have written, is
            // another mod's file -- left alone, as hsie.ini sites are.
            boat.State = HoldsApeWrite(boat) ? ZonState.Applied : ZonState.ChangedByOtherMod;
            if (boat.State == ZonState.ChangedByOtherMod)
                boat.Problem = "it no longer holds the crush speed APE wrote";
        }

        static bool HoldsApeWrite(UBoatZon boat)
        {
            byte[] now = boat.Bytes, orig = boat.Original;
            if (now.Length != orig.Length) return false;
            int at = boat.Record.CrashSpeedAt;
            for (int i = 0; i < now.Length; i++)
            {
                if (i >= at && i < at + 4) continue;
                if (now[i] != orig[i]) return false;
            }
            float written = BitConverter.ToSingle(now, at);
            // APE's own value, or the original's (a crush time that happens to
            // reproduce it, or a revert whose sidecar delete failed).
            return Zon.CrushTimeOf(boat.Record.HitPoints, written) > 0
                || written == boat.Record.CrashSpeed;
        }
    }
}
