// PROTOTYPE -- THROWAWAY. Not production code. No tests, no error handling, no write path.
//
// Variants of the APE patcher's main window, switchable from a floating bottom bar,
// in throwaway C# WinForms (.NET Framework 4.x, csc.exe).
//
//   A -- Feature list + detail pane   (hsieOptions-style master/detail, one global Apply)
//   B -- Stacked feature cards        (each feature applies and reverts itself; no global Apply)
//   C -- Review-and-commit dashboard  (install trust header + plain-English review list)
//
// C WON the prototype round (2026-08-17) and is the default. A and B are kept only
// until C is folded into the real tool, then the whole directory goes to a throwaway branch.
// Decisions taken on C, all applied below:
//   1. band steps by 0.01 per click, not 0.05
//   2. the derived Val10 is noise -- removed from the UI entirely (still written, never shown)
//   3. "feature off" vs "APE off" must be explicit, not implied
//   4. per-line write targets (sh3.exe / hsie.ini) are a distraction -- removed; the review
//      list speaks in game terms only
//   5. one commit, not per-feature apply
//
// The bar carries two SIMULATION toggles, because the two states hardest to see are
// the ones that are hard to reach in real life:
//   [Unsupported build]  -> allowlist refusal (v1 admits OAE 1.5.2 / h.sie v 1.6E1 only)
//   [h.sie diesel flag]  -> OFF by default in every surveyed install, so the
//                           disabled-with-explanation path is the DEFAULT, not an edge case.
//
// Nothing here touches a file. Apply/Revert pop a MessageBox describing the write.
//
// Sources: .scratch/umbrella-roadmap/issues/{03,04,05,07,08,09}, docs/re-notes/{03,04}-*.md

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ApePatcherPrototype
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string v = "C";
            bool diesel = false, unsupported = false;
            foreach (string a in args)
            {
                if (a.StartsWith("--variant=")) v = a.Substring(10).ToUpperInvariant();
                if (a.Length == 1) v = a.ToUpperInvariant();   // bare "A" / "B" / "C"
                if (a == "--diesel") diesel = true;            // start with h.sie's flag simulated ON
                if (a == "--unsupported") unsupported = true;  // start on the allowlist refusal
            }
            Application.Run(new Shell(v, diesel, unsupported));
        }
    }

    // ---------------------------------------------------------------- model

    class Model
    {
        // simulated environment
        public bool SimUnsupported = false;
        public bool SimDieselFlagOn = false;

        // hull repair (ticket 03): h.sie rolls (rand(0..15)+5) * 0.01 * maxHull at 0x633633
        public bool HullApplied = false;
        public int HullMin = 5;
        public int HullMax = 20;

        // diesel damage (ticket 04)
        public bool DieselApplied = false;
        public double Rate = 1.5;      // events per 10,000 h
        public double BandLo = 0.15;
        public double BandHi = 0.55;

        public string InstallPath { get { return @"C:\games\OneAlex Extended 1.5.2"; } }
        public string BuildName { get { return SimUnsupported ? "OAE 1.5.3.7" : "OAE 1.5.2"; } }
        public string HsieVersion { get { return SimUnsupported ? "v 1.6B1" : "v 1.6E1"; } }
        public string Sha { get { return SimUnsupported ? "9f2ab77c\u2026" : "51d14ca3\u2026"; } }
        public bool Supported { get { return !SimUnsupported; } }
        public bool DieselAvailable { get { return SimDieselFlagOn; } }

        public bool HullOff { get { return HullMin == 0 && HullMax == 0; } }
        public bool HullValid { get { return HullMin >= 0 && HullMax >= HullMin && HullMax <= 100; } }

        public double MeanDamage { get { return (BandLo + BandHi) / 2.0; } }
        public bool BandValid { get { return 0 < BandLo && BandLo < BandHi && BandHi < 1.0; } }
        public bool RateValid { get { return Rate > 0.0 && Rate <= 1000.0; } }

        // p_dso, per game-second, from "events per 10,000 h"
        public double Pdso { get { return Rate / (10000.0 * 3600.0); } }
        // derived manifest site: idle cutoff, recomputed on every write. NEVER shown (decision 2).
        public double Val10 { get { return Pdso * 0.977237; } }
        // float32 lattice floor that forced the 21 -> 38 byte rewrite at 0x635818
        public bool UnderF32Epsilon { get { return Pdso < 2.98e-8; } }

        // P(two unrepaired hits sum to >= 1.0), X,Y ~ U(lo,hi)
        public double PTwoHitsDestroy
        {
            get
            {
                double lo = BandLo, hi = BandHi, w = hi - lo, s = 1.0;
                if (w <= 0) return hi * 2 >= s ? 100.0 : 0.0;
                if (s <= 2 * lo) return 100.0;
                if (s >= 2 * hi) return 0.0;
                if (s >= lo + hi) { double t = 2 * hi - s; return 100.0 * (t * t) / (2 * w * w); }
                double u = s - 2 * lo; return 100.0 * (1.0 - (u * u) / (2 * w * w));
            }
        }
    }

    // ---------------------------------------------------------------- shell + prototype bar

    class Shell : Form
    {
        static readonly string[] Keys = { "A", "B", "C" };
        static string VariantName(string k)
        {
            if (k == "A") return "Feature list + detail pane";
            if (k == "B") return "Stacked feature cards";
            return "Review-and-commit dashboard  (chosen)";
        }

        readonly Model m = new Model();
        string variant;
        Panel host;
        Label lblVariant;

        public Shell(string v, bool diesel, bool unsupported)
        {
            variant = Array.IndexOf(Keys, v) >= 0 ? v : "C";
            m.SimDieselFlagOn = diesel;
            m.SimUnsupported = unsupported;
            Text = "APE Patch Extensions \u2014 PROTOTYPE";
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(1010, 700);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 620);
            BackColor = SystemColors.Control;

            host = new Panel();
            host.Dock = DockStyle.Fill;
            Controls.Add(host);
            Controls.Add(BuildBar());
            Render();
        }

        Panel BuildBar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 36;
            bar.BackColor = Color.FromArgb(28, 28, 32);
            bar.Padding = new Padding(8, 0, 8, 0);

            Button prev = BarButton("\u25C0");
            prev.Click += delegate { Cycle(-1); };
            Button next = BarButton("\u25B6");
            next.Click += delegate { Cycle(+1); };

            lblVariant = new Label();
            lblVariant.ForeColor = Color.Gainsboro;
            lblVariant.AutoSize = false;
            lblVariant.Width = 300;
            lblVariant.Dock = DockStyle.Left;
            lblVariant.TextAlign = ContentAlignment.MiddleLeft;
            lblVariant.Padding = new Padding(10, 0, 0, 0);

            CheckBox simBuild = SimBox("sim: unsupported build", m.SimUnsupported);
            simBuild.CheckedChanged += delegate { m.SimUnsupported = simBuild.Checked; Render(); };
            CheckBox simFlag = SimBox("sim: h.sie diesel flag ON", m.SimDieselFlagOn);
            simFlag.CheckedChanged += delegate { m.SimDieselFlagOn = simFlag.Checked; Render(); };

            Label note = new Label();
            note.Text = "\u2190 \u2192 cycles";
            note.ForeColor = Color.FromArgb(120, 120, 130);
            note.Dock = DockStyle.Right;
            note.AutoSize = false;
            note.Width = 90;
            note.TextAlign = ContentAlignment.MiddleRight;

            bar.Controls.Add(note);
            bar.Controls.Add(simFlag);
            bar.Controls.Add(simBuild);
            bar.Controls.Add(lblVariant);
            bar.Controls.Add(next);
            bar.Controls.Add(prev);
            return bar;
        }

        Button BarButton(string glyph)
        {
            Button b = new Button();
            b.Text = glyph;
            b.Dock = DockStyle.Left;
            b.Width = 38;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.ForeColor = Color.White;
            b.BackColor = Color.FromArgb(52, 52, 60);
            b.TabStop = false;
            return b;
        }

        CheckBox SimBox(string text, bool initial)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.Checked = initial;          // set before the handler is wired
            c.ForeColor = Color.FromArgb(200, 190, 120);
            c.Dock = DockStyle.Left;
            c.AutoSize = false;
            c.Width = 178;
            c.Padding = new Padding(12, 0, 0, 0);
            c.TabStop = false;
            return c;
        }

        void Cycle(int d)
        {
            int i = Array.IndexOf(Keys, variant);
            i = (i + d + Keys.Length) % Keys.Length;
            variant = Keys[i];
            Render();
        }

        void Render()
        {
            lblVariant.Text = variant + " \u2014 " + VariantName(variant);
            host.SuspendLayout();
            host.Controls.Clear();
            Control c;
            if (variant == "A") c = new VariantA(m, Render);
            else if (variant == "B") c = new VariantB(m, Render);
            else c = new VariantC(m, Render);
            c.Dock = DockStyle.Fill;
            host.Controls.Add(c);
            host.ResumeLayout();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            bool typing = ActiveControl is TextBoxBase
                          || (ActiveControl != null && ActiveControl.Parent is NumericUpDown);
            if (!typing)
            {
                if (keyData == System.Windows.Forms.Keys.Left) { Cycle(-1); return true; }
                if (keyData == System.Windows.Forms.Keys.Right) { Cycle(+1); return true; }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ---------------------------------------------------------------- shared primitives (deliberately tiny)

    static class UI
    {
        public static readonly Font Mono = new Font("Consolas", 8.5f);
        public static readonly Color Warn = Color.FromArgb(150, 60, 20);
        public static readonly Color Dim = Color.FromArgb(110, 110, 110);
        public static readonly Color Good = Color.FromArgb(30, 110, 50);

        public static Label L(string text, int x, int y, int w, bool dim)
        {
            Label l = new Label();
            l.Text = text; l.Left = x; l.Top = y; l.Width = w; l.Height = 17;
            if (dim) l.ForeColor = Dim;
            return l;
        }

        public static NumericUpDown Num(decimal val, decimal min, decimal max, int dp, decimal inc, int x, int y, int w)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max; n.DecimalPlaces = dp; n.Increment = inc;
            n.Value = val < min ? min : (val > max ? max : val);
            n.Left = x; n.Top = y; n.Width = w;
            n.TextAlign = HorizontalAlignment.Right;
            return n;
        }

        // data/art/blinkingape.png, embedded at build time by run.cmd (/resource:...,ape.png).
        // Derived from data/art/"BlinkingApe (OA modder).tga" -- flat grey field keyed out and
        // trimmed. The real tool ships it the same way: one exe, nothing loose to lose.
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

        public static string Sci(double d)
        {
            return d.ToString("0.0000e-00", CultureInfo.InvariantCulture);
        }

        public static string F2(double d) { return d.ToString("0.00", CultureInfo.InvariantCulture); }
        public static string F1(double d) { return d.ToString("0.0", CultureInfo.InvariantCulture); }

        public static void Stub(string what)
        {
            MessageBox.Show(what, "PROTOTYPE \u2014 no bytes were written",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Derived-stat lines shared by all variants (the numbers, not the layout).
        // Val10 is deliberately absent -- decision 2.
        public static string[] DieselStats(Model m)
        {
            // kept under ~50 chars: the left column of variant C is only 430px wide
            return new string[] {
                "Mean damage per event   " + F2(m.MeanDamage) + " of the engine",
                "Two unrepaired hits     " + F1(m.PTwoHitsDestroy) + "% destroy it outright",
                "Wear rate               " + Sci(m.Pdso) + " per game-second"
            };
        }
    }

    // ---------------------------------------------------------------- A: feature list + detail pane

    class VariantA : Panel
    {
        readonly Model m;
        readonly Action refresh;
        string selected = "hull";
        Panel detail;

        public VariantA(Model model, Action refreshShell)
        {
            m = model; refresh = refreshShell;
            Padding = new Padding(0);

            Panel strip = new Panel();
            strip.Dock = DockStyle.Top; strip.Height = 30;
            strip.BackColor = m.Supported ? Color.FromArgb(238, 240, 238) : Color.FromArgb(250, 226, 216);
            Label s = new Label();
            s.Dock = DockStyle.Fill; s.TextAlign = ContentAlignment.MiddleLeft;
            s.Padding = new Padding(10, 0, 0, 0);
            s.Text = m.Supported
                ? m.InstallPath + "    \u2022  " + m.BuildName + "  \u2022  h.sie " + m.HsieVersion + "  \u2022  sha " + m.Sha + "  \u2022  supported"
                : m.InstallPath + "    \u2022  " + m.BuildName + "  \u2022  h.sie " + m.HsieVersion + "  \u2022  UNSUPPORTED BUILD \u2014 APE writes nothing";
            if (!m.Supported) { s.ForeColor = UI.Warn; s.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold); }
            strip.Controls.Add(s);

            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom; foot.Height = 46; foot.Padding = new Padding(10, 8, 10, 8);
            Button apply = new Button();
            apply.Text = "Apply changes"; apply.Width = 130; apply.Height = 28;
            apply.Dock = DockStyle.Right; apply.Enabled = m.Supported;
            apply.Click += delegate { UI.Stub(PendingText()); };
            Label hint = new Label();
            hint.Dock = DockStyle.Fill; hint.ForeColor = UI.Dim;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Text = "Unchecking a feature restores the original bytes. There is no uninstaller \u2014 that is the uninstaller.";
            foot.Controls.Add(hint);
            foot.Controls.Add(apply);

            Panel list = new Panel();
            list.Dock = DockStyle.Left; list.Width = 236; list.BackColor = Color.White;
            list.Controls.Add(Row("diesel", "Diesel damage", m.DieselApplied, m.DieselAvailable, 44));
            list.Controls.Add(Row("hull", "Hull repair at sea", m.HullApplied, true, 0));

            detail = new Panel();
            detail.Dock = DockStyle.Fill; detail.Padding = new Padding(16, 12, 16, 12);

            Controls.Add(detail);
            Controls.Add(list);
            Controls.Add(foot);
            Controls.Add(strip);
            BuildDetail();
        }

        Control Row(string key, string title, bool applied, bool available, int top)
        {
            Panel p = new Panel();
            p.Left = 0; p.Top = top; p.Width = 236; p.Height = 44;
            p.BackColor = selected == key ? Color.FromArgb(222, 232, 245) : Color.White;
            CheckBox cb = new CheckBox();
            cb.Left = 10; cb.Top = 12; cb.Width = 18; cb.Checked = applied;
            cb.Enabled = available && m.Supported;
            cb.CheckedChanged += delegate
            {
                if (key == "hull") m.HullApplied = cb.Checked; else m.DieselApplied = cb.Checked;
                refresh();
            };
            Label t = new Label();
            t.Left = 32; t.Top = 5; t.Width = 196; t.Text = title;
            Label sub = new Label();
            sub.Left = 32; sub.Top = 22; sub.Width = 196; sub.ForeColor = UI.Dim;
            sub.Text = !available ? "unavailable \u2014 h.sie patch off"
                     : applied ? "APE applied" : "h.sie stock behaviour";
            if (!available) { t.ForeColor = UI.Dim; }
            EventHandler pick = delegate { selected = key; refresh(); };
            p.Click += pick; t.Click += pick; sub.Click += pick;
            p.Controls.Add(sub); p.Controls.Add(t); p.Controls.Add(cb);
            return p;
        }

        void BuildDetail()
        {
            detail.Controls.Clear();
            if (selected == "hull") BuildHull(); else BuildDiesel();
        }

        void BuildHull()
        {
            detail.Controls.Add(Head("Hull repair at sea",
                "h.sie's U-Tanker patch lets the crew repair the hull underway. APE narrows the roll."));
            int y = 76;
            detail.Controls.Add(UI.L("Repair per event, as a percentage of maximum hull:", 0, y, 460, false));
            y += 26;
            detail.Controls.Add(UI.L("Minimum", 0, y + 3, 60, false));
            NumericUpDown lo = UI.Num((decimal)m.HullMin, 0, 100, 0, 1, 66, y, 66);
            NumericUpDown hi = UI.Num((decimal)m.HullMax, 0, 100, 0, 1, 176, y, 66);
            detail.Controls.Add(UI.L("Maximum", 142, y + 3, 60, false));
            detail.Controls.Add(UI.L("%", 248, y + 3, 20, true));
            lo.ValueChanged += delegate { m.HullMin = (int)lo.Value; refresh(); };
            hi.ValueChanged += delegate { m.HullMax = (int)hi.Value; refresh(); };
            lo.Enabled = hi.Enabled = m.HullApplied && m.Supported;
            detail.Controls.Add(lo); detail.Controls.Add(hi);
            y += 34;
            detail.Controls.Add(UI.L("h.sie stock is 5\u201320%. Both zero = APE applied but inert.", 0, y, 520, true));
            y += 30;
            if (!m.HullValid)
                detail.Controls.Add(Warn("Minimum must not exceed maximum.", y));
            else if (m.HullApplied && m.HullOff)
                detail.Controls.Add(Warn("Feature off, but APE is still applied \u2014 not the same as unchecking.", y));
            y += 34;
            detail.Controls.Add(Box("Writes to", new string[] {
                "sh3.exe  0x633633   roll operands (h.sie .pcode)"
            }, y, 560));
        }

        void BuildDiesel()
        {
            detail.Controls.Add(Head("Diesel damage",
                "Diesels wear out on their own. A rare, permanent hit any time they are turning."));
            int y = 76;

            if (!m.DieselAvailable)
            {
                Panel warn = new Panel();
                warn.Left = 0; warn.Top = y; warn.Width = 600; warn.Height = 72;
                warn.BackColor = Color.FromArgb(252, 244, 226);
                warn.Controls.Add(UI.L("This patch needs h.sie's own \u201cDiesel Damage\u201d option switched on.", 12, 10, 570, false));
                Label how = UI.L("It is off in your install. Turn it on with HsieOptionsSelector, then reopen APE. "
                    + "APE never writes h.sie's own switches.", 12, 30, 570, true);
                how.Height = 34;
                warn.Controls.Add(how);
                detail.Controls.Add(warn);
                y += 84;
            }

            bool on = m.DieselAvailable && m.DieselApplied && m.Supported;

            detail.Controls.Add(UI.L("How often", 0, y, 200, false));
            y += 22;
            NumericUpDown rate = UI.Num((decimal)m.Rate, 0.1m, 1000m, 1, 0.1m, 0, y, 80);
            rate.Enabled = on;
            rate.ValueChanged += delegate { m.Rate = (double)rate.Value; refresh(); };
            detail.Controls.Add(rate);
            detail.Controls.Add(UI.L("events per 10,000 hours of diesel running", 88, y + 3, 320, true));
            y += 34;

            detail.Controls.Add(UI.L("How bad", 0, y, 200, false));
            y += 22;
            NumericUpDown blo = UI.Num((decimal)m.BandLo, 0.01m, 0.99m, 2, 0.01m, 0, y, 70);
            NumericUpDown bhi = UI.Num((decimal)m.BandHi, 0.01m, 0.99m, 2, 0.01m, 96, y, 70);
            blo.Enabled = bhi.Enabled = on;
            blo.ValueChanged += delegate { m.BandLo = (double)blo.Value; refresh(); };
            bhi.ValueChanged += delegate { m.BandHi = (double)bhi.Value; refresh(); };
            detail.Controls.Add(blo);
            detail.Controls.Add(UI.L("to", 76, y + 3, 20, true));
            detail.Controls.Add(bhi);
            detail.Controls.Add(UI.L("of one engine, per event", 176, y + 3, 260, true));
            y += 32;
            if (!m.BandValid) { detail.Controls.Add(Warn("Needs 0 < minimum < maximum < 1.", y)); }
            y += 30;

            detail.Controls.Add(Box("What that means", UI.DieselStats(m), y, 560));
            y += 90;
            if (m.UnderF32Epsilon)
                detail.Controls.Add(Warn("Below float32 epsilon (2.98e-08) \u2014 only works because APE rewrites the helper at 0x635818.", y));
            y += 30;
            detail.Controls.Add(Box("Writes to", new string[] {
                "hsie.ini  Val9\u2013Val16          rate, exponent, idle cutoff",
                "sh3.exe   0x6351B1/B6/C3/CA    damage band immediates",
                "sh3.exe   0x635818             probability helper, 21 \u2192 38 bytes"
            }, y, 560));
        }

        Control Head(string title, string sub)
        {
            Panel p = new Panel();
            p.Left = 0; p.Top = 0; p.Width = 600; p.Height = 60;
            Label t = new Label();
            t.Text = title; t.Left = 0; t.Top = 0; t.Width = 560; t.Height = 26;
            t.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f);
            Label s = UI.L(sub, 0, 30, 580, true);
            s.Height = 22;
            p.Controls.Add(s); p.Controls.Add(t);
            return p;
        }

        Control Warn(string text, int y)
        {
            Label l = UI.L(text, 0, y, 620, false);
            l.ForeColor = UI.Warn;
            return l;
        }

        Control Box(string title, string[] lines, int y, int w)
        {
            GroupBox g = new GroupBox();
            g.Text = title; g.Left = 0; g.Top = y; g.Width = w; g.Height = 26 + lines.Length * 18;
            for (int i = 0; i < lines.Length; i++)
            {
                Label l = UI.L(lines[i], 12, 20 + i * 18, w - 20, false);
                l.Font = UI.Mono;
                g.Controls.Add(l);
            }
            return g;
        }

        string PendingText()
        {
            string s = "";
            if (m.HullApplied) s += "sh3.exe 0x633633  hull repair " + m.HullMin + "\u2013" + m.HullMax + "%\r\n";
            if (m.DieselApplied && m.DieselAvailable)
                s += "hsie.ini Val9\u2013Val16 + sh3.exe 5 sites  diesel damage\r\n";
            return s.Length == 0 ? "Nothing to apply." : s;
        }
    }

    // ---------------------------------------------------------------- B: stacked feature cards

    class VariantB : Panel
    {
        readonly Model m;
        readonly Action refresh;

        public VariantB(Model model, Action refreshShell)
        {
            m = model; refresh = refreshShell;
            BackColor = Color.FromArgb(246, 246, 248);

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.Padding = new Padding(18, 16, 18, 16);

            if (!m.Supported) flow.Controls.Add(Refusal());
            flow.Controls.Add(HullCard());
            flow.Controls.Add(DieselCard());

            Panel foot = new Panel();
            foot.Dock = DockStyle.Bottom; foot.Height = 26;
            Label f = new Label();
            f.Dock = DockStyle.Fill; f.ForeColor = UI.Dim; f.Padding = new Padding(18, 0, 0, 0);
            f.Text = m.InstallPath + "  \u2022  " + m.BuildName + "  \u2022  h.sie " + m.HsieVersion + "  \u2022  " + m.Sha;
            foot.Controls.Add(f);

            Controls.Add(flow);
            Controls.Add(foot);
        }

        Control Refusal()
        {
            Panel p = Card(80);
            p.BackColor = Color.FromArgb(250, 226, 216);
            Label t = UI.L("Unsupported build \u2014 nothing can be written", 18, 14, 500, false);
            t.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            t.ForeColor = UI.Warn;
            Label s = UI.L("APE v1 supports OAE 1.5.2 with h.sie v 1.6E1 only. This install reports "
                + m.BuildName + " / " + m.HsieVersion + ".", 18, 38, 780, true);
            s.Height = 30;
            p.Controls.Add(s); p.Controls.Add(t);
            return p;
        }

        Panel Card(int h)
        {
            Panel p = new Panel();
            p.Width = 900; p.Height = h;
            p.BackColor = Color.White;
            p.Margin = new Padding(0, 0, 0, 14);
            p.BorderStyle = BorderStyle.FixedSingle;
            return p;
        }

        void Header(Panel card, string title, string chip, Color chipColor, bool checkedState, bool enabled, EventHandler onToggle)
        {
            CheckBox cb = new CheckBox();
            cb.Left = 16; cb.Top = 18; cb.Width = 18;
            cb.Checked = checkedState; cb.Enabled = enabled;
            cb.CheckedChanged += onToggle;
            Label t = UI.L(title, 40, 13, 440, false);
            t.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 12f);
            t.Height = 24;
            Label c = UI.L(chip, 700, 17, 180, false);
            c.TextAlign = ContentAlignment.MiddleRight;
            c.ForeColor = chipColor;
            card.Controls.Add(c); card.Controls.Add(t); card.Controls.Add(cb);
        }

        void Buttons(Panel card, int y, bool enabled, string applyText, EventHandler apply, EventHandler revert)
        {
            Button a = new Button();
            a.Text = applyText; a.Left = 700; a.Top = y; a.Width = 180; a.Height = 28;
            a.Enabled = enabled; a.Click += apply;
            Button r = new Button();
            r.Text = "Restore original bytes"; r.Left = 508; r.Top = y; r.Width = 180; r.Height = 28;
            r.Enabled = enabled; r.Click += revert;
            card.Controls.Add(a); card.Controls.Add(r);
        }

        Control HullCard()
        {
            Panel p = Card(190);
            Header(p, "Hull repair at sea",
                m.HullApplied ? (m.HullOff ? "applied, set to nothing" : "applied") : "h.sie stock",
                m.HullApplied ? (m.HullOff ? UI.Warn : UI.Good) : UI.Dim,
                m.HullApplied, m.Supported,
                delegate(object s, EventArgs e) { m.HullApplied = ((CheckBox)s).Checked; refresh(); });

            p.Controls.Add(UI.L("The crew repairs the hull underway. APE narrows h.sie's roll.", 40, 40, 600, true));

            bool on = m.HullApplied && m.Supported;
            NumericUpDown lo = UI.Num(m.HullMin, 0, 100, 0, 1, 40, 76, 66);
            NumericUpDown hi = UI.Num(m.HullMax, 0, 100, 0, 1, 150, 76, 66);
            lo.Enabled = hi.Enabled = on;
            lo.ValueChanged += delegate { m.HullMin = (int)lo.Value; refresh(); };
            hi.ValueChanged += delegate { m.HullMax = (int)hi.Value; refresh(); };
            p.Controls.Add(UI.L("to", 126, 79, 20, true));
            p.Controls.Add(UI.L("% of maximum hull per repair event   (h.sie stock: 5\u201320)", 226, 79, 420, true));
            p.Controls.Add(lo); p.Controls.Add(hi);

            string note = !m.HullValid ? "Minimum must not exceed maximum."
                : m.HullOff ? "Both zero: APE stays applied and the crew repairs nothing. Uncheck instead to give h.sie its roll back."
                : "Mean repair " + UI.F1((m.HullMin + m.HullMax) / 2.0) + "% per event.";
            Label n = UI.L(note, 40, 112, 620, m.HullValid && !m.HullOff);
            if (!m.HullValid || m.HullOff) n.ForeColor = UI.Warn;
            p.Controls.Add(n);

            Buttons(p, 140, on && m.HullValid, "Apply hull repair",
                delegate { UI.Stub("sh3.exe 0x633633 \u2014 roll operands \u2192 " + m.HullMin + "\u2013" + m.HullMax + "%"); },
                delegate { UI.Stub("sh3.exe 0x633633 \u2014 restore h.sie's original operands (5\u201320%)"); });
            return p;
        }

        Control DieselCard()
        {
            if (!m.DieselAvailable)
            {
                Panel u = Card(126);
                Header(u, "Diesel damage", "unavailable", UI.Dim, false, false, delegate { });
                Label a = UI.L("h.sie's own \u201cDiesel Damage\u201d switch is off in this install, so there is nothing "
                    + "for APE to tune \u2014 the code it writes into would never run.", 40, 42, 820, true);
                a.Height = 20;
                Label b = UI.L("Switch it on in HsieOptionsSelector and reopen APE. APE reads h.sie's switches "
                    + "but never writes them.", 40, 64, 820, true);
                b.Height = 20;
                u.Controls.Add(a); u.Controls.Add(b);
                return u;
            }

            Panel p = Card(300);
            Header(p, "Diesel damage", m.DieselApplied ? "applied" : "h.sie stock",
                m.DieselApplied ? UI.Good : UI.Dim, m.DieselApplied, m.Supported,
                delegate(object s, EventArgs e) { m.DieselApplied = ((CheckBox)s).Checked; refresh(); });
            p.Controls.Add(UI.L("Diesels wear out on their own \u2014 rare, permanent, any time they are turning.", 40, 40, 620, true));

            bool on = m.DieselApplied && m.Supported;

            NumericUpDown rate = UI.Num((decimal)m.Rate, 0.1m, 1000m, 1, 0.1m, 40, 76, 80);
            rate.Enabled = on;
            rate.ValueChanged += delegate { m.Rate = (double)rate.Value; refresh(); };
            p.Controls.Add(rate);
            p.Controls.Add(UI.L("events per 10,000 hours running", 128, 79, 300, true));

            NumericUpDown blo = UI.Num((decimal)m.BandLo, 0.01m, 0.99m, 2, 0.01m, 40, 110, 70);
            NumericUpDown bhi = UI.Num((decimal)m.BandHi, 0.01m, 0.99m, 2, 0.01m, 136, 110, 70);
            blo.Enabled = bhi.Enabled = on;
            blo.ValueChanged += delegate { m.BandLo = (double)blo.Value; refresh(); };
            bhi.ValueChanged += delegate { m.BandHi = (double)bhi.Value; refresh(); };
            p.Controls.Add(blo); p.Controls.Add(bhi);
            p.Controls.Add(UI.L("to", 116, 113, 20, true));
            p.Controls.Add(UI.L("of one engine per event", 216, 113, 300, true));

            string[] stats = UI.DieselStats(m);
            for (int i = 0; i < stats.Length; i++)
            {
                Label l = UI.L(stats[i], 40, 148 + i * 18, 700, false);
                l.Font = UI.Mono;
                if (i == 1 && m.PTwoHitsDestroy > 1.0) l.ForeColor = UI.Warn;
                p.Controls.Add(l);
            }
            if (!m.BandValid)
            {
                Label bad = UI.L("Needs 0 < minimum < maximum < 1.", 40, 210, 500, false);
                bad.ForeColor = UI.Warn; p.Controls.Add(bad);
            }
            else if (m.UnderF32Epsilon)
            {
                Label eps = UI.L("Below float32 epsilon \u2014 reachable only because APE rewrites the helper at 0x635818.",
                    40, 210, 700, true);
                p.Controls.Add(eps);
            }

            Buttons(p, 250, on && m.BandValid && m.RateValid, "Apply diesel damage",
                delegate { UI.Stub("hsie.ini  Val9\u2013Val16 (Val10 derived = " + UI.Sci(m.Val10) + ")\r\n"
                    + "sh3.exe   0x6351B1/B6/C3/CA  band " + UI.F2(m.BandLo) + "\u2013" + UI.F2(m.BandHi) + "\r\n"
                    + "sh3.exe   0x635818           21 \u2192 38 byte rewrite"); },
                delegate { UI.Stub("Restore hsie.ini Val9\u2013Val16 lines and 5 sh3.exe sites to their recorded originals."); });
            return p;
        }
    }

    // ---------------------------------------------------------------- C: review-and-commit dashboard (CHOSEN)

    class VariantC : Panel
    {
        readonly Model m;
        readonly Action refresh;

        public VariantC(Model model, Action refreshShell)
        {
            m = model; refresh = refreshShell;
            BackColor = Color.FromArgb(242, 243, 245);

            Controls.Add(RightColumn());
            Controls.Add(LeftColumn());
            Controls.Add(Verify());
            Controls.Add(Commit());
        }

        Control Verify()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top; p.Height = 110;
            p.BackColor = m.Supported ? Color.White : Color.FromArgb(250, 226, 216);

            Control art;
            Image img = UI.Art();
            if (img != null)
            {
                PictureBox pb = new PictureBox();
                pb.Image = img;
                pb.SizeMode = PictureBoxSizeMode.Zoom;   // art is 187x251, portrait
                pb.BackColor = Color.Transparent;
                pb.Left = 16; pb.Top = 8; pb.Width = 70; pb.Height = 94;
                art = pb;
            }
            else
            {
                Panel ph = new Panel();
                ph.Left = 16; ph.Top = 8; ph.Width = 70; ph.Height = 94;
                ph.BorderStyle = BorderStyle.FixedSingle;
                ph.BackColor = Color.FromArgb(236, 237, 240);
                Label t0 = UI.L("art", 0, 38, 70, true);
                t0.TextAlign = ContentAlignment.MiddleCenter;
                ph.Controls.Add(t0);
                art = ph;
            }

            Label brand = UI.L("APE Patch Extensions", 100, 16, 300, false);
            brand.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 13f);
            brand.Height = 26;

            Label t = UI.L(m.Supported ? "Install verified" : "Install refused", 100, 48, 300, false);
            t.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            if (!m.Supported) t.ForeColor = UI.Warn;
            Label sub = UI.L(m.Supported
                ? "APE will only write to a build it recognises."
                : "APE writes nothing to a build it does not recognise.", 100, 68, 300, true);

            string[] rows = m.Supported
                ? new string[] {
                    "path      " + m.InstallPath,
                    "build     " + m.BuildName + "   h.sie " + m.HsieVersion + "   sha " + m.Sha,
                    "backups   sh3_BACKUP.exe, hsie_BACKUP.ini present"
                  }
                : new string[] {
                    "path      " + m.InstallPath,
                    "build     " + m.BuildName + "   h.sie " + m.HsieVersion + "   sha " + m.Sha,
                    "expected  OAE 1.5.2 / h.sie v 1.6E1"
                  };
            for (int i = 0; i < rows.Length; i++)
            {
                Label l = UI.L(rows[i], 430, 28 + i * 18, 580, false);
                l.Font = UI.Mono;
                if (!m.Supported && i == 2) l.ForeColor = UI.Warn;
                p.Controls.Add(l);
            }
            p.Controls.Add(sub);
            p.Controls.Add(t);
            p.Controls.Add(brand);
            p.Controls.Add(art);
            return p;
        }

        Control LeftColumn()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Left; p.Width = 430; p.Padding = new Padding(18, 14, 10, 14);
            p.BackColor = Color.FromArgb(242, 243, 245);

            // ---- hull repair
            int y = 12;
            p.Controls.Add(Toggle("Hull repair at sea", m.HullApplied, m.Supported, y,
                delegate(object s, EventArgs e) { m.HullApplied = ((CheckBox)s).Checked; refresh(); }));
            y += 26;
            p.Controls.Add(State(m.HullApplied
                ? "APE is applied. Unchecking puts h.sie's own 5\u201320% roll back."
                : "Not applied \u2014 h.sie's own 5\u201320% roll is untouched.", y));
            y += 24;

            NumericUpDown lo = UI.Num(m.HullMin, 0, 100, 0, 1, 40, y, 60);
            NumericUpDown hi = UI.Num(m.HullMax, 0, 100, 0, 1, 128, y, 60);
            lo.Enabled = hi.Enabled = m.HullApplied && m.Supported;
            lo.ValueChanged += delegate { m.HullMin = (int)lo.Value; refresh(); };
            hi.ValueChanged += delegate { m.HullMax = (int)hi.Value; refresh(); };
            p.Controls.Add(lo); p.Controls.Add(hi);
            p.Controls.Add(UI.L("to", 106, y + 3, 20, true));
            p.Controls.Add(UI.L("% hull per event", 196, y + 3, 200, true));
            y += 28;

            if (m.HullApplied && !m.HullValid)
                p.Controls.Add(Bad("Minimum must not exceed maximum.", y));
            else if (m.HullApplied && m.HullOff)
                p.Controls.Add(Bad("Feature off, APE still applied \u2014 the crew repairs nothing.", y));
            y += 30;

            // ---- diesel damage
            p.Controls.Add(Toggle("Diesel damage", m.DieselApplied, m.DieselAvailable && m.Supported, y,
                delegate(object s, EventArgs e) { m.DieselApplied = ((CheckBox)s).Checked; refresh(); }));
            y += 26;

            if (!m.DieselAvailable)
            {
                p.Controls.Add(State("Unavailable \u2014 h.sie's own Diesel Damage switch is off.", y));
                y += 22;
                p.Controls.Add(State("Turn it on in HsieOptionsSelector, then reopen APE.", y));
                return p;
            }

            p.Controls.Add(State(m.DieselApplied
                ? "APE is applied. Unchecking puts h.sie's own damage model back."
                : "Not applied \u2014 h.sie's own damage model is untouched.", y));
            y += 24;

            NumericUpDown rate = UI.Num((decimal)m.Rate, 0.1m, 1000m, 1, 0.1m, 40, y, 70);
            rate.Enabled = m.DieselApplied && m.Supported;
            rate.ValueChanged += delegate { m.Rate = (double)rate.Value; refresh(); };
            p.Controls.Add(rate);
            p.Controls.Add(UI.L("events / 10,000 h", 118, y + 3, 200, true));
            y += 30;

            NumericUpDown blo = UI.Num((decimal)m.BandLo, 0.01m, 0.99m, 2, 0.01m, 40, y, 62);
            NumericUpDown bhi = UI.Num((decimal)m.BandHi, 0.01m, 0.99m, 2, 0.01m, 128, y, 62);
            blo.Enabled = bhi.Enabled = m.DieselApplied && m.Supported;
            blo.ValueChanged += delegate { m.BandLo = (double)blo.Value; refresh(); };
            bhi.ValueChanged += delegate { m.BandHi = (double)bhi.Value; refresh(); };
            p.Controls.Add(blo); p.Controls.Add(bhi);
            p.Controls.Add(UI.L("to", 106, y + 3, 20, true));
            p.Controls.Add(UI.L("engine per event", 196, y + 3, 200, true));
            y += 30;

            if (m.DieselApplied && !m.BandValid)
            {
                p.Controls.Add(Bad("Needs 0 < minimum < maximum < 1.", y));
                return p;
            }
            y += 8;

            string[] stats = UI.DieselStats(m);
            for (int i = 0; i < stats.Length; i++)
            {
                Label l = UI.L(stats[i], 34, y + i * 17, 398, true);
                l.Font = UI.Mono;
                if (i == 1 && m.PTwoHitsDestroy > 5.0) l.ForeColor = UI.Warn;
                p.Controls.Add(l);
            }
            y += stats.Length * 17 + 6;
            if (m.UnderF32Epsilon)
                p.Controls.Add(State("That rate is below float32 epsilon \u2014 it only works because APE "
                    + "replaces h.sie's probability helper.", y));
            return p;
        }

        Control Toggle(string title, bool state, bool enabled, int y, EventHandler h)
        {
            Panel row = new Panel();
            row.Left = 0; row.Top = y; row.Width = 400; row.Height = 24;
            CheckBox cb = new CheckBox();
            cb.Left = 12; cb.Top = 3; cb.Width = 18; cb.Checked = state; cb.Enabled = enabled;
            cb.CheckedChanged += h;
            Label t = UI.L(title, 34, 4, 340, false);
            t.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            if (!enabled) t.ForeColor = UI.Dim;
            row.Controls.Add(t); row.Controls.Add(cb);
            return row;
        }

        Control State(string text, int y)
        {
            Label l = UI.L(text, 34, y, 390, true);
            return l;
        }

        Control Bad(string text, int y)
        {
            Label l = UI.L(text, 34, y, 390, false);
            l.ForeColor = UI.Warn;
            return l;
        }

        // The review list speaks in game terms only -- no file names, no addresses (decision 4).
        Control RightColumn()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill; p.Padding = new Padding(10, 14, 18, 14);
            p.BackColor = Color.FromArgb(242, 243, 245);

            int features = 0;
            if (m.Supported && m.HullApplied) features++;
            if (m.Supported && m.DieselApplied && m.DieselAvailable) features++;

            Label h = UI.L("What will change", 4, 10, 300, false);
            h.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 11f);
            p.Controls.Add(h);
            Label hc = UI.L(features == 0 ? "" : features + (features == 1 ? " feature" : " features"), 300, 13, 220, true);
            hc.TextAlign = ContentAlignment.MiddleRight;
            p.Controls.Add(hc);

            Panel list = new Panel();
            list.Left = 4; list.Top = 36; list.Width = 520; list.Height = 420;
            list.BackColor = Color.White; list.BorderStyle = BorderStyle.FixedSingle;
            list.AutoScroll = true;
            int y = 12;

            if (!m.Supported)
            {
                Label n = UI.L("Nothing \u2014 this build is not one APE knows how to write.", 16, y, 480, false);
                n.ForeColor = UI.Warn;
                list.Controls.Add(n);
            }
            else if (features == 0)
            {
                list.Controls.Add(UI.L("Nothing. Both features are left on h.sie's own behaviour.", 16, y, 480, true));
            }
            else
            {
                if (m.HullApplied)
                {
                    list.Controls.Add(Group("Hull repair at sea", y)); y += 24;
                    if (m.HullOff)
                    {
                        y = Line("The crew will repair nothing at sea. APE stays applied \u2014 "
                            + "unchecking the feature instead gives h.sie's 5\u201320% roll back.", y, list, true);
                    }
                    else
                    {
                        y = Line("The crew repairs " + m.HullMin + " to " + m.HullMax
                            + "% of maximum hull each time they succeed, instead of h.sie's 5 to 20%.", y, list, false);
                    }
                    y += 10;
                }
                if (m.DieselApplied && m.DieselAvailable)
                {
                    list.Controls.Add(Group("Diesel damage", y)); y += 24;
                    y = Line("A diesel takes damage about " + UI.F1(m.Rate)
                        + " times per 10,000 hours of running, at any speed.", y, list, false);
                    y = Line("Each hit costs " + UI.F2(m.BandLo) + " to " + UI.F2(m.BandHi)
                        + " of one engine. Two hits left unrepaired destroy it "
                        + UI.F1(m.PTwoHitsDestroy) + "% of the time, and a destroyed engine "
                        + "cannot be repaired at sea.", y, list, m.PTwoHitsDestroy > 5.0);
                    y = Line("Diesels below one tenth telegraph count as idle and take no wear.", y, list, false);
                    y += 10;
                }
                list.Controls.Add(UI.L("Nothing else is touched, including any hsieOptions choices.", 16, y + 4, 490, true));
            }
            p.Controls.Add(list);
            return p;
        }

        Control Group(string title, int y)
        {
            Label l = UI.L(title, 16, y, 480, false);
            l.Font = new Font(SystemFonts.MessageBoxFont, FontStyle.Bold);
            return l;
        }

        // wraps; returns the next free y
        int Line(string text, int y, Panel list, bool warn)
        {
            Label t = new Label();
            t.Text = "\u2022  " + text;
            t.Left = 16; t.Top = y; t.Width = 480;
            t.AutoSize = false;
            t.Height = 18 * (1 + text.Length / 78);
            if (warn) t.ForeColor = UI.Warn;
            list.Controls.Add(t);
            return y + t.Height + 4;
        }

        Control Commit()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Bottom; p.Height = 56; p.Padding = new Padding(18, 12, 18, 12);
            p.BackColor = Color.White;

            bool hull = m.Supported && m.HullApplied && m.HullValid;
            bool diesel = m.Supported && m.DieselApplied && m.DieselAvailable && m.BandValid && m.RateValid;

            Button b = new Button();
            b.Dock = DockStyle.Right; b.Width = 220; b.Height = 32;
            b.Text = (hull || diesel) ? "Apply changes" : "Nothing to apply";
            b.Enabled = hull || diesel;
            b.Click += delegate
            {
                string s = "";
                if (hull) s += "sh3.exe  0x633633            hull " + m.HullMin + "\u2013" + m.HullMax + "%\r\n";
                if (diesel)
                {
                    s += "hsie.ini Val9\u2013Val16           rate " + UI.F1(m.Rate)
                       + " / 10,000 h  (Val10 derived = " + UI.Sci(m.Val10) + ")\r\n";
                    s += "sh3.exe  0x6351B1/B6/C3/CA   band " + UI.F2(m.BandLo) + "\u2013" + UI.F2(m.BandHi) + "\r\n";
                    s += "sh3.exe  0x635818            probability helper, 21 \u2192 38 bytes\r\n";
                }
                UI.Stub(s + "\r\nOriginal bytes are verified at every site before any of it is written.");
            };

            Label note = new Label();
            note.Dock = DockStyle.Fill; note.ForeColor = UI.Dim;
            note.TextAlign = ContentAlignment.MiddleLeft;
            note.Text = "Original bytes are verified before each write. Unchecking a feature puts them back \u2014 "
                + "that is the uninstaller.";
            p.Controls.Add(note);
            p.Controls.Add(b);
            return p;
        }
    }
}
