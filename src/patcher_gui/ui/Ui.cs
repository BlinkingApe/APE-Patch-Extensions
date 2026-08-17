// Shared drawing primitives for the one window.
//
// Two things here are load-bearing rather than decorative:
//
//   * every wrapped line is measured through TextRenderer, the same path that
//     draws it, so nothing is clipped by a long install path or a long warning
//     at any DPI scale;
//   * the brand label draws several runs at different weights, and measures each
//     of them the same way, because a hand-summed x-offset drifts on a scaled
//     display and would turn a quiet touch into a visible defect.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ApePatcher.Ui
{
    static class Look
    {
        public static readonly Color Warn = Color.FromArgb(150, 60, 20);
        public static readonly Color Dim = Color.FromArgb(110, 110, 110);
        public static readonly Color Good = Color.FromArgb(30, 110, 50);
        public static readonly Color Page = Color.FromArgb(242, 243, 245);
        public static readonly Color Refused = Color.FromArgb(250, 226, 216);

        public static readonly Font Base = SystemFonts.MessageBoxFont;
        public static readonly Font Bold = new Font(Base, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 8.5f);
        public static readonly Font Brand = new Font(Base.FontFamily, 13f);
        public static readonly Font BrandBold = new Font(Base.FontFamily, 13f, FontStyle.Bold);
        public static readonly Font Heading = new Font(Base.FontFamily, 11f);

        /// <summary>Segoe UI Semibold is a separate family, not a FontStyle.
        /// Resolved once; null when the platform does not have it, which drops the
        /// brand label to a single tier rather than faking one.</summary>
        public static readonly Font BrandSemibold = ResolveSemibold(13f);

        static Font ResolveSemibold(float size)
        {
            try
            {
                Font f = new Font("Segoe UI Semibold", size, FontStyle.Regular);
                // A missing family silently falls back to the default one, so
                // check we actually got what we asked for.
                return f.FontFamily.Name == "Segoe UI Semibold" ? f : null;
            }
            catch
            {
                return null;
            }
        }

        // data/art/blinkingape.png, embedded at build time. One exe, nothing loose
        // to lose.
        static Image art;
        static bool artTried;

        public static Image Art()
        {
            if (artTried) return art;
            artTried = true;
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ape.png");
                if (s != null) art = Image.FromStream(s);
            }
            catch { }
            return art;
        }

        public static Icon AppIcon()
        {
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ape.ico");
                if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }
    }

    static class Widgets
    {
        public static Label Label(string text, Font font, Color colour)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = colour;
            l.AutoSize = true;
            l.Margin = new Padding(0, 0, 0, 2);
            return l;
        }

        /// <summary>A label whose height is measured, not estimated, so long text
        /// wraps instead of being clipped.</summary>
        public static Label Wrapped(string text, Font font, Color colour, int width)
        {
            Label l = new Label();
            l.Font = font;
            l.ForeColor = colour;
            l.AutoSize = false;
            l.Width = width;
            SetWrappedText(l, text, width);
            return l;
        }

        public static void SetWrappedText(Label l, string text, int width)
        {
            l.Text = text;
            l.Width = width;
            l.Height = Measure(text, l.Font, width);
        }

        public static int Measure(string text, Font font, int width)
        {
            Size size = TextRenderer.MeasureText(text, font,
                new Size(Math.Max(16, width), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return size.Height + 2;
        }

        public static NumericUpDown Num(decimal value, decimal min, decimal max,
                                        int decimals, decimal step, int width)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.DecimalPlaces = decimals;
            n.Increment = step;
            n.Value = value < min ? min : (value > max ? max : value);
            n.Width = width;
            n.TextAlign = HorizontalAlignment.Right;
            return n;
        }

        /// <summary>A spinner for the diesel rate field: five orders of magnitude
        /// separate the shipping default (1.5) from the test ceiling (360,000), so
        /// a fixed additive step is unusable at one end or the other. The up/down
        /// buttons and arrow keys move proportionally instead -- about 40 clicks
        /// end to end -- while typing an exact value directly still works exactly
        /// as any NumericUpDown's text box does (ticket 01 story 3).</summary>
        public static NumericUpDown RateSpinner(decimal value, decimal min, decimal max, int width)
        {
            RateUpDown n = new RateUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.DecimalPlaces = 2;
            n.Increment = 0.1m;   // used only as the smallest step floor
            n.Value = value < min ? min : (value > max ? max : value);
            n.Width = width;
            n.TextAlign = HorizontalAlignment.Right;
            return n;
        }
    }

    /// <summary>Proportional step spinner backing <see cref="Widgets.RateSpinner"/>.
    /// See that method's comment for why a linear step does not work here.</summary>
    class RateUpDown : NumericUpDown
    {
        const decimal Factor = 1.5m;

        public override void UpButton()
        {
            Value = ClampToRange(Step(Value, Factor));
        }

        public override void DownButton()
        {
            Value = ClampToRange(Step(Value, 1m / Factor));
        }

        decimal Step(decimal v, decimal factor)
        {
            decimal next = v * factor;
            decimal min = Increment;   // the smallest step, so 0.1 does not stall at 0.1
            return factor > 1m
                ? Math.Max(next, v + min)
                : Math.Min(next, v - min);
        }

        decimal ClampToRange(decimal v)
        {
            return v < Minimum ? Minimum : (v > Maximum ? Maximum : v);
        }
    }

    /// <summary>The header's product name, drawn in runs.
    ///
    /// APE Patch Extensions expands into itself. The label sets APE in bold and
    /// the P and E of "Patch Extensions" a step lighter, so the mapping is
    /// available to a reader who looks twice and invisible to one who does not.
    /// Weight only -- no colour, no size, no spacing change -- and never
    /// explained anywhere in the tool. If the semibold face is unavailable the
    /// second tier is dropped entirely rather than approximated: a plain brand
    /// beats a loud one.</summary>
    class BrandLabel : Control
    {
        struct Run
        {
            public string Text;
            public Font Font;
            public Run(string text, Font font) { Text = text; Font = font; }
        }

        readonly Run[] runs;

        public BrandLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;

            Font second = Look.BrandSemibold ?? Look.Brand;
            runs = new Run[]
            {
                new Run("APE", Look.BrandBold),
                new Run(" ", Look.Brand),
                new Run("P", second),
                new Run("atch ", Look.Brand),
                new Run("E", second),
                new Run("xtensions", Look.Brand),
            };
            Size = PreferredSize;
        }

        public new Size PreferredSize
        {
            get
            {
                int w = 0, h = 0;
                foreach (Run r in runs)
                {
                    Size s = TextRenderer.MeasureText(r.Text, r.Font, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    w += s.Width;
                    h = Math.Max(h, s.Height);
                }
                return new Size(w + 4, h + 4);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int x = 0;
            foreach (Run r in runs)
            {
                // Measure and draw through the same path, with the same flags, so
                // the runs stay correctly spaced at every DPI scale.
                Size s = TextRenderer.MeasureText(e.Graphics, r.Text, r.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, r.Text, r.Font, new Point(x, 0), ForeColor,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                x += s.Width;
            }
        }
    }
}
