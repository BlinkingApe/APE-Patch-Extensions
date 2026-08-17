// Surgical hsie.ini editing.
//
// h.sie's hsie.act reads this file at load and writes each ValN over its .pconst
// address, which is why the diesel rate is routed here rather than into sh3.exe
// (umbrella ticket 09).  APE owns eight of the file's keys and must leave
// everything else -- OAE's heavily customised Vals, h.sie's comment blocks, the
// parameter table, the line endings, even a stray trailing tab -- exactly as it
// found them.
//
// So the file is held as a list of lines with their own terminators, decoded
// through Latin-1, which round-trips every byte.  A write replaces one line's
// text and touches nothing else.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ApePatcher
{
    public class IniFile
    {
        // Latin-1 maps every byte 0..255 to a distinct char and back, so the file
        // survives a load/save round trip untouched whatever it actually holds.
        static readonly Encoding ByteExact = Encoding.GetEncoding(28591);

        class Row
        {
            public string Text;        // the line, without its terminator
            public string Terminator;  // "\r\n", "\n", or "" at a final line with no newline
            public string Key;         // null for comments and blanks
        }

        readonly List<Row> rows = new List<Row>();

        public static IniFile Load(string path)
        {
            return Parse(ByteExact.GetString(File.ReadAllBytes(path)));
        }

        public static IniFile Parse(string text)
        {
            IniFile ini = new IniFile();
            int i = 0;
            while (i < text.Length)
            {
                int nl = text.IndexOf('\n', i);
                string line, term;
                if (nl < 0)
                {
                    line = text.Substring(i);
                    term = "";
                    i = text.Length;
                }
                else
                {
                    bool crlf = nl > i && text[nl - 1] == '\r';
                    line = text.Substring(i, (crlf ? nl - 1 : nl) - i);
                    term = crlf ? "\r\n" : "\n";
                    i = nl + 1;
                }
                Row r = new Row();
                r.Text = line;
                r.Terminator = term;
                r.Key = KeyOf(line);
                ini.rows.Add(r);
            }
            return ini;
        }

        /// <summary>The key a line declares, or null if it is a comment, a
        /// section header, or blank.</summary>
        static string KeyOf(string line)
        {
            int start = 0;
            while (start < line.Length && (line[start] == ' ' || line[start] == '\t')) start++;
            if (start >= line.Length) return null;
            if (line[start] == ';' || line[start] == '#' || line[start] == '[') return null;
            int eq = line.IndexOf('=', start);
            if (eq < 0) return null;
            string key = line.Substring(start, eq - start).TrimEnd(' ', '\t');
            return key.Length == 0 ? null : key;
        }

        Row Find(string key)
        {
            // Exact key match, never a prefix: "Val1" must not find "Val10".
            foreach (Row r in rows)
                if (r.Key != null && string.Equals(r.Key, key, StringComparison.Ordinal))
                    return r;
            return null;
        }

        public bool Has(string key) { return Find(key) != null; }

        /// <summary>The complete line as it stands, without its terminator. This
        /// is what the manifest stores for exact-text revert.</summary>
        public string Line(string key)
        {
            Row r = Find(key);
            return r == null ? null : r.Text;
        }

        /// <summary>The value to the right of the '=', trimmed of surrounding
        /// whitespace but otherwise verbatim.</summary>
        public string Value(string key)
        {
            Row r = Find(key);
            if (r == null) return null;
            int eq = r.Text.IndexOf('=');
            return r.Text.Substring(eq + 1).Trim(' ', '\t');
        }

        public bool TryNumber(string key, out double value)
        {
            value = 0.0;
            string raw = Value(key);
            return raw != null && Fmt.TryParse(raw, out value);
        }

        public IEnumerable<string> Keys()
        {
            foreach (Row r in rows) if (r.Key != null) yield return r.Key;
        }

        /// <summary>Replace one key's complete line. The key must already exist:
        /// APE edits h.sie's file, it never grows it.</summary>
        public void SetLine(string key, string completeLine)
        {
            Row r = Find(key);
            if (r == null)
                throw new PatchException("hsie.ini has no " + key + " line to write.");
            if (!string.Equals(KeyOf(completeLine), key, StringComparison.Ordinal))
                throw new PatchException("Refusing to write a " + key + " line that declares "
                    + (KeyOf(completeLine) ?? "no key") + ".");
            r.Text = completeLine;
        }

        public string Render()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Row r in rows) sb.Append(r.Text).Append(r.Terminator);
            return sb.ToString();
        }

        public void Save(string path)
        {
            File.WriteAllBytes(path, ByteExact.GetBytes(Render()));
        }
    }
}
