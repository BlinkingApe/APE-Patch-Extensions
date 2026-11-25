// Prepared game directories for the session tests.
//
// APE's real target is a 1.4 MB commercial binary that cannot be checked in, so
// the sh3.exe fixture is synthesised: a file of the right length holding h.sie's
// own bytes at exactly the offsets the manifest names, plus the flag table and
// the version stamp.  It is built *from the manifest*, so it is stock by
// construction and cannot drift from what the tool expects.
//
// Ticket 13: APE no longer identifies a specific sh3.exe build, so there is
// nothing left to admit -- a stock fixture is ready to open the moment its
// bytes are in place.
//
// fixtures/hsie.ini is a synthetic stand-in, not a copy of any real OAE
// install: it keeps the structural quirks the tests depend on (CRLF line
// endings, key order, comment lines flanking the internal-values block,
// trailing tabs on Val13/14/15) with placeholder values everywhere except
// the eight keys the manifest names, which match the manifest's "original"
// text exactly. fixtures/en_menu.txt is a trimmed stand-in for
// data/Menu/en_menu.txt (ticket 10): only string 4893 and its neighbours,
// since that is the only line APE's diesel-message site touches.

using System;
using System.Collections.Generic;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class Fixtures
    {
        public const int Sh3ExeLength = 1417216;

        // The live OAE 1.5.2 flag table, read 2026-08-15 (docs/re-notes/03, sec.2).
        // Entry 20 -- Diesel Damage Fix -- is the one the tool reads.
        static readonly byte[] FlagTable = Hex.ToBytes(
            "ffffff000000ffffffffffffffffffffffffff000000");

        static string root;

        public static string RepoRoot()
        {
            if (root != null) return root;
            string dir = Path.GetDirectoryName(
                new Uri(typeof(Fixtures).Assembly.CodeBase).LocalPath);
            while (dir != null && !File.Exists(Path.Combine(dir, "CLAUDE.md")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null) throw new Exception("Cannot find the repository root from the test binary.");
            root = dir;
            return root;
        }

        public static string HsieIniPath()
        {
            return Path.Combine(RepoRoot(), @"tests\patcher_gui\fixtures\hsie.ini");
        }

        public static string MenuTextPath()
        {
            return Path.Combine(RepoRoot(), @"tests\patcher_gui\fixtures\en_menu.txt");
        }

        public static string Latin1(byte[] bytes)
        {
            return System.Text.Encoding.GetEncoding(28591).GetString(bytes);
        }

        // -- temp directories ---------------------------------------------------

        static readonly List<string> made = new List<string>();

        public static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "ape-patcher-tests",
                                      Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            made.Add(dir);
            return dir;
        }

        public static string CopyHsieIni()
        {
            string path = Path.Combine(TempDir(), "hsie.ini");
            File.Copy(HsieIniPath(), path);
            return path;
        }

        // -- a prepared game directory -----------------------------------------

        public class Install
        {
            public string Dir;
            public Manifest Manifest;
            public string Sh3Exe { get { return Path.Combine(Dir, "sh3.exe"); } }
            public string HsieIni { get { return Path.Combine(Dir, "hsie.ini"); } }
            public string MenuText { get { return Path.Combine(Dir, "data", "Menu", "en_menu.txt"); } }

            public byte[] ExeBytes() { return File.ReadAllBytes(Sh3Exe); }
            public string IniText() { return Latin1(File.ReadAllBytes(HsieIni)); }
            public string MenuTextContent() { return Latin1(File.ReadAllBytes(MenuText)); }

            /// <summary>The ape.act template this install's sessions deploy. Null
            /// means the one embedded in the test binary, which run.cmd embeds
            /// from the same Release build the shipped exe does.</summary>
            public byte[] Template;

            public byte[] ApeActTemplate()
            {
                if (Template != null) return Template;
                byte[] t = DepthStress.EmbeddedTemplate();
                if (t == null) throw new Exception("The test binary was built without ape.act.");
                return t;
            }

            public PatchSession Session() { return PatchSession.Open(Dir, Manifest, ApeActTemplate()); }
        }

        /// <summary>A game directory holding h.sie's own bytes everywhere APE
        /// writes -- i.e. an install with neither feature applied.</summary>
        public static Install Stock(bool dieselFlagOn)
        {
            return StockBuild(dieselFlagOn, "v 1.6E1");
        }

        /// <summary>The same shape of install, stamped with whatever h.sie
        /// version string is given. Ticket 13: APE reads and displays this
        /// stamp but never matches it against a list, so a stamp that is not
        /// OAE 1.5.2's own (e.g. KC 2.5's "v 1.6B1") opens exactly the same
        /// way -- there is no separate admission step left to exercise.</summary>
        public static Install StockBuild(bool dieselFlagOn, string hsieVersionStamp)
        {
            Install install = new Install();
            install.Dir = TempDir();
            install.Manifest = Manifest.Embedded();

            byte[] exe = BuildStockExe(install.Manifest, dieselFlagOn, hsieVersionStamp);
            File.WriteAllBytes(install.Sh3Exe, exe);
            File.Copy(HsieIniPath(), install.HsieIni);
            Directory.CreateDirectory(Path.GetDirectoryName(install.MenuText));
            File.Copy(MenuTextPath(), install.MenuText);

            return install;
        }

        /// <summary>The same stock shape, but with one sh3.exe site's invariant
        /// context byte corrupted. This is what a genuinely incompatible
        /// install looks like now that there is no whole-file identity check
        /// (ticket 13): Read() still opens it -- sh3.exe, hsie.ini and
        /// en_menu.txt are all present and readable -- and it is Verify() that
        /// catches the mismatch and refuses the write.</summary>
        public static Install ContextCorrupted()
        {
            Install install = Stock(true);
            Site site = install.Manifest.Patch(Manifest.HullPatchId).Sites[0];   // hull-roll-width
            KeyValuePair<int, byte[]> ctx = site.Context[0];

            byte[] exe = install.ExeBytes();
            exe[site.Offset + ctx.Key] ^= 0xFF;
            File.WriteAllBytes(install.Sh3Exe, exe);
            return install;
        }

        // -- U-boat .zon files (depth-pressure-stress ticket 16) --------------

        /// <summary>Adds data/Submarine/folder/folder.cfg and .zon to an install.
        /// humanPlayable null leaves the key out of [Unit], as KC's AI classes do.</summary>
        public static string AddUBoat(Install install, string folder, string humanPlayable,
                                      float hitPoints, float crashSpeed)
        {
            return AddUBoat(install, folder, folder + ".cfg", humanPlayable, hitPoints, crashSpeed);
        }

        public static string AddUBoat(Install install, string folder, string cfgName,
                                      string humanPlayable, float hitPoints, float crashSpeed)
        {
            string dir = Path.Combine(Path.Combine(Path.Combine(install.Dir, "data"), "Submarine"), folder);
            Directory.CreateDirectory(dir);
            string cfg = "[Unit]\r\nClassName=SSType" + folder + "\r\n"
                + (humanPlayable == null ? "" : "HumanPlayable=" + humanPlayable + "\r\n")
                + "UnitType=200\r\n\r\n[Properties]\r\nCrashDepth=80;meters\r\n";
            File.WriteAllText(Path.Combine(dir, cfgName), cfg);
            string zon = Path.Combine(dir, folder + ".zon");
            File.WriteAllBytes(zon, Zon(hitPoints, crashSpeed));
            return zon;
        }

        /// <summary>A synthetic .zon in the layout ticket 18 found in all 42 real
        /// files: the controller name, then its data block, whose records are
        /// u32 length, key, NUL, float32. ArmorLevel repeats, as it does in the
        /// real files, which is why APE must not anchor on it.</summary>
        public static byte[] Zon(float hitPoints, float crashSpeed)
        {
            MemoryStream m = new MemoryStream();
            Raw(m, new byte[] { 0x5A, 0x4F, 0x4E, 0x01, 0x00, 0x00, 0x00, 0x07 });
            Text(m, "ColisionableObject\0");
            Raw(m, new byte[] { 0x10, 0x00, 0x00, 0x00, 0xAB, 0xCD });
            Text(m, "ColisionableObject\0");
            Record(m, "ArmorLevel", 2f);
            Record(m, "Hit_Points", hitPoints);
            Record(m, "CrashDepth", 331f);
            Record(m, "CrashSpeed", crashSpeed);
            Record(m, "Rebound", 0.5f);
            Raw(m, new byte[] { 0x00, 0x11, 0x22, 0x33 });
            Record(m, "ArmorLevel", 3f);
            Record(m, "ArmorLevel", 4f);
            return m.ToArray();
        }

        static void Record(MemoryStream m, string key, float value)
        {
            Raw(m, BitConverter.GetBytes((uint)(key.Length + 1 + 4)));
            Text(m, key + "\0");
            Raw(m, BitConverter.GetBytes(value));
        }

        static void Text(MemoryStream m, string s) { Raw(m, System.Text.Encoding.ASCII.GetBytes(s)); }
        static void Raw(MemoryStream m, byte[] b) { m.Write(b, 0, b.Length); }

        // -- data/Zones.cfg (depth-pressure-stress ticket 33) -----------------

        /// <summary>A trimmed Zones.cfg in OAE's shape: the electric engine room,
        /// then both motors' zones, CRLF, with a commented-out Father line in
        /// another section to catch a parser that doesn't respect sections or
        /// comments. stbdFather is [ElectricEngine1]'s Father: "24" in OAE/GWX,
        /// "30" in vanilla.</summary>
        public static string ZonesCfgText(string stbdFather)
        {
            return ";GWX Damage Model\r\n"
                + "[Zones List]\r\n22=ElectricEngine1\r\n24=Quarters1\r\n30=ElectricEngineRoom\r\n56=ElectricEngine2\r\n\r\n"
                + "[ElectricEngineRoom]\r\nCategory=Engines Room\r\nHitPoints=400\r\n;Father=24\r\n\r\n"
                + "[ElectricEngine1]\r\nCategory=Engines Room\r\nHitPoints=180\r\nCritical=No\r\n"
                + "Father=" + stbdFather + "\r\nFloodingTime=59.999996\r\n\r\n"
                + "[ElectricEngine2]\r\nCategory=Engines Room\r\nHitPoints=170\r\nFather=30\r\n";
        }

        /// <summary>Writes data/Zones.cfg into an install; returns its path.</summary>
        public static string AddZonesCfg(Install install, string stbdFather)
        {
            string path = Path.Combine(Path.Combine(install.Dir, "data"), "Zones.cfg");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes(ZonesCfgText(stbdFather)));
            return path;
        }

        /// <summary>The CrashSpeed a .zon currently holds, found the same way APE
        /// finds it.</summary>
        public static float CrashSpeedIn(string zonPath)
        {
            string problem;
            ZonRecord r = ApePatcher.Zon.Parse(File.ReadAllBytes(zonPath), out problem);
            if (r == null) throw new Exception(zonPath + ": " + problem);
            return r.CrashSpeed;
        }

        static byte[] BuildStockExe(Manifest m, bool dieselFlagOn, string hsieVersionStamp)
        {
            byte[] exe = new byte[Sh3ExeLength];

            System.Text.Encoding.ASCII.GetBytes(hsieVersionStamp)
                .CopyTo(exe, m.VersionStringOffset);

            byte[] flags = (byte[])FlagTable.Clone();
            Patch diesel = m.Patch(Manifest.DieselPatchId);
            flags[diesel.FlagIndex - 1] = dieselFlagOn ? (byte)0xFF : (byte)0x00;
            flags.CopyTo(exe, m.FlagTableOffset);

            foreach (Patch p in m.Patches)
                foreach (Site s in p.Sites)
                {
                    if (s.Store != SiteStore.Sh3Exe) continue;
                    s.Original.CopyTo(exe, s.Offset);
                }
            return exe;
        }
    }
}
