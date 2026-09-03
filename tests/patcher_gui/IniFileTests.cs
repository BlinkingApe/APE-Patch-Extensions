// hsie.ini surgery.
//
// APE never ships a replacement hsie.ini: OAE has heavily customised the Vals
// APE does not own (torpedo failure, weather, snorkelling, collision), and h.sie
// files the diesel keys under a "NEVER DO ANY CHANGES BELOW THIS LINE" banner
// whose surrounding text has to survive intact.  So every assertion here is
// about what did *not* change.
//
// The fixture is a synthetic stand-in, not OAE's real file -- but it keeps
// the shape that matters to these tests: CRLF line endings, trailing tabs
// on Val13/14/15, comment blocks around the internal-values keys.

using System;
using System.IO;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class IniFileTests
    {
        public static void Test_ReadsAValueByKey()
        {
            IniFile ini = IniFile.Load(Fixtures.HsieIniPath());
            Check.Equal("2e-4", ini.Value("Val9"), "Val9 as shipped");
            Check.Equal("1e-6", ini.Value("Val10"), "Val10 as shipped");
            Check.Equal("200.0", ini.Value("Val14"), "a trailing tab is not part of the value");
        }

        public static void Test_ReadsTheCompleteOriginalLineIncludingTrailingWhitespace()
        {
            // The manifest stores the complete line so that unchecking restores
            // the file byte for byte -- including Val13's trailing tab.
            IniFile ini = IniFile.Load(Fixtures.HsieIniPath());
            Check.Equal("Val13=8.0\t", ini.Line("Val13"), "Val13's line, tab and all");
            Check.Equal("Val9=2e-4", ini.Line("Val9"), "Val9's line");
        }

        public static void Test_MissingKeyIsReportedNotInvented()
        {
            IniFile ini = IniFile.Load(Fixtures.HsieIniPath());
            Check.False(ini.Has("Val99"), "Val99 is not in the file");
            Check.Equal(null, ini.Value("Val99"), "and reading it yields nothing");
        }

        public static void Test_ReplacingOneLineLeavesEveryOtherByteAlone()
        {
            string path = Fixtures.CopyHsieIni();
            byte[] before = File.ReadAllBytes(path);

            IniFile ini = IniFile.Load(path);
            ini.SetLine("Val9", "Val9=4.1667e-08");
            ini.Save(path);

            byte[] after = File.ReadAllBytes(path);
            string b = Fixtures.Latin1(before), a = Fixtures.Latin1(after);

            Check.Contains("Val9=4.1667e-08\r\n", a, "the new line is written, CRLF preserved");
            Check.DoesNotContain("Val9=2e-4", a, "the old line is gone");
            Check.Equal(b.Replace("Val9=2e-4", "Val9=4.1667e-08"), a,
                "nothing but that one line changed");
        }

        public static void Test_CommentsOrderingAndForeignKeysSurviveAFullDieselWrite()
        {
            string path = Fixtures.CopyHsieIni();
            string before = Fixtures.Latin1(File.ReadAllBytes(path));

            IniFile ini = IniFile.Load(path);
            ini.SetLine("Val9", "Val9=4.1667e-08");
            ini.SetLine("Val10", "Val10=4.0718e-08");
            ini.SetLine("Val11", "Val11=1.0");
            ini.SetLine("Val12", "Val12=1.0");
            ini.SetLine("Val13", "Val13=1.0");
            ini.SetLine("Val14", "Val14=0.01");
            ini.SetLine("Val15", "Val15=0.01");
            ini.SetLine("Val16", "Val16=0.01");
            ini.Save(path);

            string after = Fixtures.Latin1(File.ReadAllBytes(path));
            IniFile back = IniFile.Load(path);

            // Every Val APE does not own is untouched -- these are OAE's own
            // customisations and losing them would be a silent regression.
            string[] foreign = { "Val1", "Val2", "Val3", "Val4", "Val5", "Val6", "Val7",
                                 "Val8", "Val17", "Val18", "Val19", "Val20", "Val21",
                                 "Val22", "Val23", "Val24", "nParam", "Adr9", "Typ9" };
            IniFile original = IniFile.Load(Fixtures.HsieIniPath());
            foreach (string key in foreign)
                Check.Equal(original.Line(key), back.Line(key), key + " is untouched");

            // Comments, banners and blank lines all survive.
            Check.Contains("; p_dso = Basic damage probability for every second", after,
                "h.sie's own commentary survives");
            Check.Contains("; 3) INTERNAL VALUES. NEVER CHANGE !!!", after,
                "the internal-values banner survives");
            Check.Equal(CountLines(before), CountLines(after), "no line was added or dropped");

            // Ordering is positional, so compare the key sequence directly.
            Check.Equal(string.Join(",", original.Keys()), string.Join(",", back.Keys()),
                "key order is unchanged");
        }

        public static void Test_KeyMatchingIsExactNotPrefixed()
        {
            // "Val1" must not match "Val10" -- the diesel block sits directly
            // below keys whose names are prefixes of each other.
            string path = Fixtures.CopyHsieIni();
            IniFile ini = IniFile.Load(path);
            ini.SetLine("Val1", "Val1=99.0");
            ini.Save(path);

            IniFile back = IniFile.Load(path);
            Check.Equal("99.0", back.Value("Val1"), "Val1 changed");
            Check.Equal("1e-6", back.Value("Val10"), "Val10 did not");
            Check.Equal("2e-4", back.Value("Val9"), "Val9 did not");
        }

        public static void Test_ValuesAreParsedInvariantlyWhateverTheCulture()
        {
            // h.sie's parser expects "4.1667e-08"; on a comma-decimal machine a
            // culture-sensitive parse would read that back as something else, or
            // fail, and the window would show the player wrong numbers.
            IniFile ini = IniFile.Load(Fixtures.HsieIniPath());
            double v;
            Check.True(ini.TryNumber("Val9", out v), "Val9 parses");
            Check.Close(2e-4, v, 1e-12, "Val9 is 2e-4 in any culture");
            Check.True(ini.TryNumber("Val14", out v), "Val14 parses despite its trailing tab");
            Check.Close(200.0, v, 1e-12, "Val14 is 200");
        }

        static int CountLines(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) if (s[i] == '\n') n++;
            return n;
        }
    }
}
