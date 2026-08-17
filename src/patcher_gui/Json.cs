// A minimal JSON reader for the embedded manifest.
//
// The tool is compiled with csc.exe against .NET Framework 4.x and ships as a
// single exe in the game root, so there is no package manager in the picture and
// no serializer worth the attribute plumbing.  This reads the one document APE
// owns: objects, arrays, strings, numbers, booleans, null.
//
// Numbers parse through the invariant culture, which matters -- a comma-decimal
// machine must read the same manifest as everyone else.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ApePatcher
{
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            object value = ParseValue(text, ref i);
            SkipWhite(text, ref i);
            if (i != text.Length) throw Bad(text, i, "trailing content");
            return value;
        }

        // -- typed accessors, so callers do not cast at every step -------------

        public static Dictionary<string, object> Obj(object o)
        {
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d == null) throw new PatchException("Manifest: expected an object.");
            return d;
        }

        public static List<object> Arr(object o)
        {
            List<object> l = o as List<object>;
            if (l == null) throw new PatchException("Manifest: expected an array.");
            return l;
        }

        public static object Get(Dictionary<string, object> o, string key)
        {
            object v;
            return o.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> o, string key)
        {
            object v = Get(o, key);
            return v as string;
        }

        public static int Int(Dictionary<string, object> o, string key)
        {
            object v = Get(o, key);
            if (v == null) throw new PatchException("Manifest: missing '" + key + "'.");
            return (int)Math.Round((double)v);
        }

        public static double Num(Dictionary<string, object> o, string key)
        {
            object v = Get(o, key);
            if (v == null) throw new PatchException("Manifest: missing '" + key + "'.");
            return (double)v;
        }

        // -- the parser --------------------------------------------------------

        static object ParseValue(string s, ref int i)
        {
            SkipWhite(s, ref i);
            if (i >= s.Length) throw Bad(s, i, "unexpected end");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            Dictionary<string, object> o = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // '{'
            SkipWhite(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                SkipWhite(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhite(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Bad(s, i, "expected ':'");
                i++;
                o[key] = ParseValue(s, ref i);
                SkipWhite(s, ref i);
                if (i >= s.Length) throw Bad(s, i, "unexpected end in object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return o; }
                throw Bad(s, i, "expected ',' or '}'");
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            List<object> a = new List<object>();
            i++; // '['
            SkipWhite(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (true)
            {
                a.Add(ParseValue(s, ref i));
                SkipWhite(s, ref i);
                if (i >= s.Length) throw Bad(s, i, "unexpected end in array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return a; }
                throw Bad(s, i, "expected ',' or ']'");
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw Bad(s, i, "expected a string");
            i++;
            StringBuilder sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw Bad(s, i, "unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Bad(s, i, "short \\u escape");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber,
                                                  CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Bad(s, i, "unknown escape '\\" + e + "'");
                }
            }
            if (i >= s.Length) throw Bad(s, i, "unterminated string");
            i++; // closing quote
            return sb.ToString();
        }

        static object ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E'
                                    || ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
                i++;
            if (i == start) throw Bad(s, i, "expected a value");
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float,
                                CultureInfo.InvariantCulture);
        }

        static bool Match(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
        }

        static PatchException Bad(string s, int i, string what)
        {
            return new PatchException("Manifest is not valid JSON at offset "
                + i.ToString(CultureInfo.InvariantCulture) + ": " + what + ".");
        }
    }
}
