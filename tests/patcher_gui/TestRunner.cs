// A very small test harness for the C# patcher.
//
// There is no test framework in this repo's C# toolchain -- the tool is built
// with csc.exe against .NET Framework 4.x and ships as one exe, so pulling in a
// NuGet-delivered runner would cost more than it buys.  This is the whole rig:
// methods named Test_* on a class are cases, an exception is a failure.
//
// Every case runs once per culture in Cultures below.  That is umbrella PRD
// requirement 7 -- a comma-decimal machine must produce byte-identical output --
// and it is why the runner, not the individual test, owns the culture loop.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ApePatcher.Tests
{
    static class Check
    {
        public static void True(bool condition, string what)
        {
            if (!condition) throw new AssertionFailed(what);
        }

        public static void False(bool condition, string what)
        {
            if (condition) throw new AssertionFailed(what);
        }

        public static void Equal(object expected, object actual, string what)
        {
            if (!Equals(expected, actual))
                throw new AssertionFailed(what + Environment.NewLine
                    + "    expected: " + Show(expected) + Environment.NewLine
                    + "    actual:   " + Show(actual));
        }

        public static void Close(double expected, double actual, double tolerance, string what)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new AssertionFailed(what + Environment.NewLine
                    + "    expected: " + expected.ToString("R", CultureInfo.InvariantCulture)
                    + " (+/- " + tolerance.ToString("R", CultureInfo.InvariantCulture) + ")"
                    + Environment.NewLine
                    + "    actual:   " + actual.ToString("R", CultureInfo.InvariantCulture));
        }

        public static void Bytes(byte[] expected, byte[] actual, string what)
        {
            if (expected.Length != actual.Length || !ByteEq(expected, actual))
                throw new AssertionFailed(what + Environment.NewLine
                    + "    expected: " + Hex(expected) + Environment.NewLine
                    + "    actual:   " + Hex(actual));
        }

        public static void Contains(string needle, string haystack, string what)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new AssertionFailed(what + Environment.NewLine
                    + "    looked for: " + needle + Environment.NewLine
                    + "    in:         " + Show(haystack));
        }

        public static void DoesNotContain(string needle, string haystack, string what)
        {
            if (haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
                throw new AssertionFailed(what + Environment.NewLine
                    + "    should not contain: " + needle + Environment.NewLine
                    + "    in:                 " + Show(haystack));
        }

        static bool ByteEq(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static string Hex(byte[] b)
        {
            if (b == null) return "<null>";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        static string Show(object o)
        {
            if (o == null) return "<null>";
            string s = o as string;
            if (s != null) return "\"" + s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            IFormattable f = o as IFormattable;
            if (f != null) return f.ToString(null, CultureInfo.InvariantCulture);
            return o.ToString();
        }
    }

    class AssertionFailed : Exception
    {
        public AssertionFailed(string message) : base(message) { }
    }

    static class Runner
    {
        // Invariant first, then a comma-decimal locale.  Anything that formats or
        // parses through the current culture breaks on the second pass.
        static readonly string[] Cultures = { "", "de-DE" };

        public static int Main(string[] args)
        {
            string filter = args.Length > 0 ? args[0] : null;
            List<MethodInfo> cases = Discover(filter);

            int failed = 0, ran = 0;
            foreach (string culture in Cultures)
            {
                CultureInfo ci = culture.Length == 0
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(culture);
                string label = culture.Length == 0 ? "invariant" : culture;
                Console.WriteLine();
                Console.WriteLine("--- culture: " + label + " ---");

                foreach (MethodInfo m in cases)
                {
                    Thread.CurrentThread.CurrentCulture = ci;
                    Thread.CurrentThread.CurrentUICulture = ci;
                    string name = m.DeclaringType.Name + "." + m.Name.Substring("Test_".Length);
                    ran++;
                    try
                    {
                        m.Invoke(null, null);
                        Console.WriteLine("  ok   " + name);
                    }
                    catch (TargetInvocationException tie)
                    {
                        failed++;
                        Exception inner = tie.InnerException ?? tie;
                        Console.WriteLine("  FAIL " + name);
                        Console.WriteLine("       " + inner.Message.Replace("\n", "\n       "));
                        if (!(inner is AssertionFailed))
                            Console.WriteLine("       " + inner.GetType().Name + Environment.NewLine
                                + Indent(inner.StackTrace));
                    }
                    finally
                    {
                        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(failed == 0
                ? string.Format(CultureInfo.InvariantCulture, "{0} passed.", ran)
                : string.Format(CultureInfo.InvariantCulture, "{0} of {1} FAILED.", failed, ran));
            return failed == 0 ? 0 : 1;
        }

        static string Indent(string s)
        {
            if (s == null) return "";
            return "       " + s.Replace("\n", "\n       ");
        }

        static List<MethodInfo> Discover(string filter)
        {
            List<MethodInfo> found = new List<MethodInfo>();
            foreach (Type t in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!t.Name.EndsWith("Tests", StringComparison.Ordinal)) continue;
                MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static);
                Array.Sort(methods, delegate(MethodInfo a, MethodInfo b)
                {
                    return string.CompareOrdinal(a.Name, b.Name);
                });
                foreach (MethodInfo m in methods)
                {
                    if (!m.Name.StartsWith("Test_", StringComparison.Ordinal)) continue;
                    if (m.GetParameters().Length != 0) continue;
                    if (filter != null && m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0
                                       && t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    found.Add(m);
                }
            }
            return found;
        }
    }
}
