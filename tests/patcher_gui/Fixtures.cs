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
// hsie.ini is the real thing -- fixtures/hsie.ini is a byte copy of OAE 1.5.2's,
// trailing tabs and all. fixtures/en_menu.txt is a trimmed stand-in for
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

            public PatchSession Session() { return PatchSession.Open(Dir, Manifest); }
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
