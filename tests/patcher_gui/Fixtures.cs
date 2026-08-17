// Prepared game directories for the session tests.
//
// APE's real target is a 1.4 MB commercial binary that cannot be checked in, so
// the sh3.exe fixture is synthesised: a file of the right length holding h.sie's
// own bytes at exactly the offsets the manifest names, plus the flag table and
// the version stamp.  It is built *from the manifest*, so it is stock by
// construction and cannot drift from what the tool expects.
//
// Its hash is then admitted to a private copy of the manifest, which is how the
// allowlist mechanism itself gets exercised honestly rather than stubbed out.
//
// hsie.ini is the real thing -- fixtures/hsie.ini is a byte copy of OAE 1.5.2's,
// trailing tabs and all.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class Fixtures
    {
        public const int Sh3ExeLength = 1417216;
        public const string TestVersionId = "test-synthetic";

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

            public byte[] ExeBytes() { return File.ReadAllBytes(Sh3Exe); }
            public string IniText() { return Latin1(File.ReadAllBytes(HsieIni)); }

            /// <summary>Open a session against this directory. Build identity is
            /// normalised over APE's own sites, so a directory APE has already
            /// written to still identifies as the same build -- no re-admission
            /// here, which is exactly the property a player depends on when they
            /// reopen the tool.</summary>
            public PatchSession Session() { return PatchSession.Open(Dir, Manifest); }
        }

        /// <summary>A game directory holding h.sie's own bytes everywhere APE
        /// writes -- i.e. an install with neither feature applied.</summary>
        public static Install Stock(bool dieselFlagOn)
        {
            return StockBuild(dieselFlagOn, "OAE 1.5.2", "v 1.6E1");
        }

        /// <summary>The same shape of install, but stamped as a different h.sie
        /// version and admitted as a second, separately-identified build sharing
        /// OAE 1.5.2's site offsets -- the same relationship KC 2.5 and OAE 1.5.3.7
        /// have to the shipped build (docs/re-notes/05-kc25-admission-evidence.md):
        /// same offsets, different identity because the version stamp differs.</summary>
        public static Install StockBuild(bool dieselFlagOn, string buildName, string hsieVersionStamp)
        {
            Install install = new Install();
            install.Dir = TempDir();
            install.Manifest = Manifest.Embedded();

            byte[] exe = BuildStockExe(install.Manifest, dieselFlagOn, hsieVersionStamp);
            File.WriteAllBytes(install.Sh3Exe, exe);
            File.Copy(HsieIniPath(), install.HsieIni);

            Admit(install.Manifest, exe, buildName, hsieVersionStamp);
            return install;
        }

        /// <summary>The same directory, mutated somewhere APE does not write, so
        /// its hash no longer matches any admitted build.</summary>
        public static Install UnrecognisedBuild()
        {
            Install install = Stock(true);
            byte[] exe = install.ExeBytes();
            exe[0x1000] ^= 0xFF;                // nowhere near any site
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
                    s.Original.CopyTo(exe, s.OffsetFor(ShippedVersionId(m)));
                }
            return exe;
        }

        public static string ShippedVersionId(Manifest m)
        {
            return m.Versions[0].Id;
        }

        /// <summary>Two installs sharing the same h.sie version (`v 1.6B1`, KC
        /// 2.5's and OAE 1.5.3.7's real stamp) but distinct build identities,
        /// both admitted into one manifest -- the shape ticket 01 story 19 tests:
        /// sharing an h.sie version must not imply sharing an allowlist entry.</summary>
        public static Install[] TwoBuildsSharingAnHsieVersion()
        {
            Install kc = StockBuild(true, "KC 2.5", "v 1.6B1");
            Install oae = StockBuild(true, "OAE 1.5.3.7", "v 1.6B1");

            // Real KC 2.5 and OAE 1.5.3.7 differ in bytes outside every site APE
            // owns and outside the flag table -- 2.87% of .pcode, per the spike
            // (docs/re-notes/05-kc25-admission-evidence.md) -- which is exactly
            // what makes them separately-identified builds despite sharing an
            // h.sie version and every offset APE reads. Nothing else about the
            // synthetic fixture varies the two, so mutate one far byte to stand
            // in for that divergence.
            byte[] exe = oae.ExeBytes();
            exe[0x2000] ^= 0xFF;
            File.WriteAllBytes(oae.Sh3Exe, exe);

            SupportedVersion second = kc.Manifest.AddVersionSharingOffsets(
                "test-synthetic-oae1537", "OAE 1.5.3.7", "v 1.6B1", ShippedVersionId(kc.Manifest));
            second.IdentitySha256 = kc.Manifest.IdentityOf(exe, "test-synthetic-oae1537");

            oae.Manifest = kc.Manifest;
            return new[] { kc, oae };
        }

        static void Admit(Manifest m, byte[] exe, string buildName, string hsieVersionStamp)
        {
            SupportedVersion v = m.AddVersionSharingOffsets(
                TestVersionId, buildName, hsieVersionStamp, ShippedVersionId(m));
            v.ReferenceSha256 = Sha256(exe);
            v.IdentitySha256 = m.IdentityOf(exe, TestVersionId);
        }

        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex.ToString(sha.ComputeHash(bytes));
        }
    }
}
