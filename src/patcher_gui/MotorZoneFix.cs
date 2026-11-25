// The starboard motor zone fix (depth-pressure-stress ticket 33).
//
// The GWX damage model's data/Zones.cfg (found in OAE 1.5.2 and GWX-KC 2.5) parents [ElectricEngine1], the
// starboard electric motor's zone, to zone 24, Quarters1 -- the bow quarters --
// where vanilla SH3 has 30, the electric engine room. The game then shows that
// motor's damage in the bow quarters, and only a repair team sent there repairs
// it. Confirmed live by changing that one value in a lab copy.
//
// APE offers to set it to 30. Like the crush-time patch's .zon files, the target
// is found, not authored: APE writes only an install whose [ElectricEngine1]
// Father is exactly 24, keeps the file as it was in Zones.cfg.ape-orig, and puts
// it back byte for byte on revert. A file that holds anything else is left alone.

using System;
using System.IO;
using System.Text;

namespace ApePatcher
{
    public enum ZonesState
    {
        Absent,             // no data/Zones.cfg: nothing to offer
        Faulty,             // [ElectricEngine1] Father=24: the fix is offered
        Applied,            // APE's fix, beside APE's pristine copy
        NotNeeded,          // Father is something else, e.g. vanilla's 30
        ChangedByOtherMod,  // differs from APE's write since APE made it
        Unreadable,         // no Father line APE can find, or an I/O error
    }

    /// <summary>data/Zones.cfg as read when the session opened.</summary>
    public class ZonesCfg
    {
        public string Path;
        public ZonesState State = ZonesState.Absent;
        public string Problem;

        /// <summary>The file now, and APE's pristine copy (null when there is none).</summary>
        public byte[] Bytes;
        public byte[] Original;

        public string OrigPath { get { return Path + MotorZoneFix.OrigSuffix; } }

        /// <summary>The install's own file: APE's copy when there is one.</summary>
        public byte[] Pristine { get { return Original ?? Bytes; } }
    }

    public static class MotorZoneFix
    {
        public const string PatchId = "motor-zone-fix";
        public const string PatchName = "Starboard motor zone fix";
        public const string RelativePath = "data/Zones.cfg";
        public const string OrigSuffix = ".ape-orig";

        const string Section = "ElectricEngine1";
        const string FaultyFather = "24";   // Quarters1, the bow quarters
        const string FixedFather = "30";    // ElectricEngineRoom

        static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        public static ZonesCfg Discover(string gameDir)
        {
            ZonesCfg z = new ZonesCfg();
            z.Path = System.IO.Path.Combine(System.IO.Path.Combine(gameDir, "data"), "Zones.cfg");
            if (!File.Exists(z.Path)) return z;
            try
            {
                z.Bytes = File.ReadAllBytes(z.Path);
                if (File.Exists(z.OrigPath)) z.Original = File.ReadAllBytes(z.OrigPath);
            }
            catch (Exception e)
            {
                z.State = ZonesState.Unreadable;
                z.Problem = e.Message;
                return z;
            }
            Classify(z);
            return z;
        }

        static void Classify(ZonesCfg z)
        {
            string value;
            if (z.Original != null)
            {
                if (FatherValueAt(z.Original, out value) < 0 || value != FaultyFather)
                {
                    z.State = ZonesState.Unreadable;
                    z.Problem = "APE's own copy, Zones.cfg" + OrigSuffix + ", is not the file APE fixed";
                    return;
                }
                if (Same(z.Bytes, Fixed(z.Original))) { z.State = ZonesState.Applied; return; }
                // The original back beside a copy (a revert whose copy didn't
                // delete): still the fault, and the next apply reuses the copy.
                if (Same(z.Bytes, z.Original)) { z.State = ZonesState.Faulty; return; }
                z.State = ZonesState.ChangedByOtherMod;
                z.Problem = "it no longer holds the fix APE wrote";
                return;
            }

            if (FatherValueAt(z.Bytes, out value) < 0)
            {
                z.State = ZonesState.Unreadable;
                z.Problem = "it has no Father line in [" + Section + "]";
                return;
            }
            z.State = value == FaultyFather ? ZonesState.Faulty : ZonesState.NotNeeded;
        }

        /// <summary>The byte offset of [ElectricEngine1]'s uncommented Father
        /// value, and the value itself (trimmed, without a trailing comment); -1
        /// when the section or the key isn't there.</summary>
        public static int FatherValueAt(byte[] file, out string value)
        {
            value = null;
            string text = Latin1.GetString(file);
            string section = "";
            int lineStart = 0;
            while (lineStart < text.Length)
            {
                int lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = text.Length;
                string line = text.Substring(lineStart, lineEnd - lineStart);
                int semi = line.IndexOf(';');
                string code = (semi >= 0 ? line.Substring(0, semi) : line).TrimEnd('\r');
                string trimmed = code.Trim();

                if (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    int close = trimmed.IndexOf(']');
                    section = close > 0 ? trimmed.Substring(1, close - 1).Trim() : "";
                }
                else if (string.Equals(section, Section, StringComparison.OrdinalIgnoreCase))
                {
                    int eq = code.IndexOf('=');
                    if (eq >= 0 && string.Equals(code.Substring(0, eq).Trim(), "Father",
                                                 StringComparison.OrdinalIgnoreCase))
                    {
                        int at = eq + 1;
                        while (at < code.Length && (code[at] == ' ' || code[at] == '\t')) at++;
                        value = code.Substring(at).Trim();
                        return lineStart + at;
                    }
                }
                lineStart = lineEnd + 1;
            }
            return -1;
        }

        /// <summary>The install's own file with [ElectricEngine1] Father set to 30.
        /// Same length: "24" and "30" are both two characters.</summary>
        public static byte[] Fixed(byte[] pristine)
        {
            string value;
            int at = FatherValueAt(pristine, out value);
            if (at < 0 || value != FaultyFather)
                throw new InvalidOperationException("Zones.cfg has no [" + Section + "] Father=" + FaultyFather);
            byte[] fixedBytes = (byte[])pristine.Clone();
            Latin1.GetBytes(FixedFather).CopyTo(fixedBytes, at);
            return fixedBytes;
        }

        static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
