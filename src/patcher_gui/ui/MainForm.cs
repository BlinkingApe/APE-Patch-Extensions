// The one window: prototype variant C, folded into the real tool.
//
// An install-verification header across the top, the two features down the left,
// a plain-English "what will change" list on the right, and one Apply that
// commits everything at once.
//
// This layer holds no logic.  It binds controls to a PatchState, asks
// PatchSession for a plan and a Review, and renders what they say.  Every number
// it shows comes from the model already formatted; nothing here computes a
// probability, an offset or an encoding.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ApePatcher.Ui
{
    class MainForm : Form
    {
        PatchSession session;
        PatchState desired;
        bool binding;               // true while controls are being set from the model

        // header
        Panel header;
        Label verdict, verdictSub;
        Label rowPath, rowBuild, rowExpected;

        // hull
        CheckBox hullOn;
        Label hullTitle, hullState, hullNote;
        NumericUpDown hullMin, hullMax;

        // diesel
        CheckBox dieselOn;
        Label dieselTitle, dieselState, dieselState2, dieselNote;
        NumericUpDown rate, bandLo, bandHi;
        FlowLayoutPanel statsBox;
        Panel dieselFields;

        // right column
        Label reviewHeading, reviewCount;
        Panel reviewList;

        // commit
        Button apply;
        Label commitNote;

        public MainForm(PatchSession opened)
        {
            session = opened;
            desired = session.Current.Clone();

            Text = "APE Patch Extensions";
            Font = Look.Base;
            Icon = Look.AppIcon();
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(1010, 700);
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Look.Page;

            Controls.Add(BuildRightColumn());
            Controls.Add(BuildLeftColumn());
            Controls.Add(BuildHeader());
            Controls.Add(BuildCommitBar());

            SetTabOrder();
            Sync();
        }

        // -- header ------------------------------------------------------------

        Control BuildHeader()
        {
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 116;
            header.BackColor = Color.White;

            Control art = BuildArt();
            art.Location = new Point(16, 10);
            header.Controls.Add(art);

            BrandLabel brand = new BrandLabel();
            brand.Location = new Point(104, 14);
            brand.ForeColor = SystemColors.ControlText;
            header.Controls.Add(brand);

            verdict = Widgets.Label("", Look.Bold, SystemColors.ControlText);
            verdict.Location = new Point(104, 50);
            header.Controls.Add(verdict);

            verdictSub = Widgets.Label("", Look.Base, Look.Dim);
            verdictSub.Location = new Point(104, 72);
            header.Controls.Add(verdictSub);

            rowPath = MonoRow(0);
            rowBuild = MonoRow(1);
            rowExpected = MonoRow(2);
            header.Controls.Add(rowPath);
            header.Controls.Add(rowBuild);
            header.Controls.Add(rowExpected);
            return header;
        }

        Label MonoRow(int index)
        {
            Label l = new Label();
            l.Font = Look.Mono;
            l.AutoSize = false;
            l.Location = new Point(436, 24 + index * 20);
            l.Size = new Size(540, 18);
            l.AutoEllipsis = true;
            return l;
        }

        Control BuildArt()
        {
            Image img = Look.Art();
            if (img != null)
            {
                PictureBox pb = new PictureBox();
                pb.Image = img;
                pb.SizeMode = PictureBoxSizeMode.Zoom;   // the art is 187x251, portrait
                pb.BackColor = Color.Transparent;
                pb.Size = new Size(72, 96);
                return pb;
            }
            Panel placeholder = new Panel();
            placeholder.Size = new Size(72, 96);
            placeholder.BorderStyle = BorderStyle.FixedSingle;
            placeholder.BackColor = Color.FromArgb(236, 237, 240);
            return placeholder;
        }

        // -- left column: the two features -------------------------------------

        Control BuildLeftColumn()
        {
            FlowLayoutPanel column = new FlowLayoutPanel();
            column.Dock = DockStyle.Left;
            column.Width = 440;
            column.FlowDirection = FlowDirection.TopDown;
            column.WrapContents = false;
            column.AutoScroll = true;
            column.Padding = new Padding(18, 14, 12, 14);
            column.BackColor = Look.Page;

            const int inner = 390;

            // ---- hull repair
            hullOn = new CheckBox();
            hullOn.AutoSize = true;
            hullOn.Text = "";
            hullOn.Margin = new Padding(0, 0, 0, 0);
            hullTitle = Widgets.Label("Hull repair at sea", Look.Bold, SystemColors.ControlText);
            column.Controls.Add(Row(hullOn, hullTitle));
            hullOn.CheckedChanged += delegate { if (!binding) { desired.HullApplied = hullOn.Checked; Sync(); } };

            hullState = Widgets.Wrapped("", Look.Base, Look.Dim, inner);
            hullState.Margin = new Padding(22, 2, 0, 4);
            column.Controls.Add(hullState);

            hullMin = Widgets.Num(desired.HullMin, 0, 100, 0, 1, 62);
            hullMax = Widgets.Num(desired.HullMax, 0, 100, 0, 1, 62);
            hullMin.ValueChanged += delegate { if (!binding) { desired.HullMin = (int)hullMin.Value; Sync(); } };
            hullMax.ValueChanged += delegate { if (!binding) { desired.HullMax = (int)hullMax.Value; Sync(); } };
            column.Controls.Add(Fields(new Control[] {
                hullMin,
                Widgets.Label("to", Look.Base, Look.Dim),
                hullMax,
                Widgets.Label("% of maximum hull per event", Look.Base, Look.Dim) }));

            hullNote = Widgets.Wrapped("", Look.Base, Look.Warn, inner);
            hullNote.Margin = new Padding(22, 2, 0, 12);
            column.Controls.Add(hullNote);

            // ---- diesel damage
            dieselOn = new CheckBox();
            dieselOn.AutoSize = true;
            dieselOn.Text = "";
            dieselTitle = Widgets.Label("Diesel damage", Look.Bold, SystemColors.ControlText);
            column.Controls.Add(Row(dieselOn, dieselTitle));
            dieselOn.CheckedChanged += delegate { if (!binding) { desired.DieselApplied = dieselOn.Checked; Sync(); } };

            dieselState = Widgets.Wrapped("", Look.Base, Look.Dim, inner);
            dieselState.Margin = new Padding(22, 2, 0, 0);
            column.Controls.Add(dieselState);

            dieselState2 = Widgets.Wrapped("", Look.Base, Look.Dim, inner);
            dieselState2.Margin = new Padding(22, 0, 0, 4);
            column.Controls.Add(dieselState2);

            rate = Widgets.Num((decimal)desired.Rate, 0.1m, 1000m, 1, 0.1m, 72);
            rate.ValueChanged += delegate { if (!binding) { desired.Rate = (double)rate.Value; Sync(); } };
            bandLo = Widgets.Num((decimal)desired.BandLo, 0.01m, 0.99m, 2, 0.01m, 64);
            bandHi = Widgets.Num((decimal)desired.BandHi, 0.01m, 0.99m, 2, 0.01m, 64);
            bandLo.ValueChanged += delegate { if (!binding) { desired.BandLo = (double)bandLo.Value; Sync(); } };
            bandHi.ValueChanged += delegate { if (!binding) { desired.BandHi = (double)bandHi.Value; Sync(); } };

            dieselFields = new Panel();
            dieselFields.AutoSize = true;
            dieselFields.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            dieselFields.Margin = new Padding(0, 0, 0, 0);
            FlowLayoutPanel stack = new FlowLayoutPanel();
            stack.FlowDirection = FlowDirection.TopDown;
            stack.WrapContents = false;
            stack.AutoSize = true;
            stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stack.Controls.Add(Fields(new Control[] {
                rate, Widgets.Label("events per 10,000 hours of running", Look.Base, Look.Dim) }));
            stack.Controls.Add(Fields(new Control[] {
                bandLo,
                Widgets.Label("to", Look.Base, Look.Dim),
                bandHi,
                Widgets.Label("of one engine per event", Look.Base, Look.Dim) }));
            dieselFields.Controls.Add(stack);
            column.Controls.Add(dieselFields);

            dieselNote = Widgets.Wrapped("", Look.Base, Look.Warn, inner);
            dieselNote.Margin = new Padding(22, 2, 0, 4);
            column.Controls.Add(dieselNote);

            statsBox = new FlowLayoutPanel();
            statsBox.FlowDirection = FlowDirection.TopDown;
            statsBox.WrapContents = false;
            statsBox.AutoSize = true;
            statsBox.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            statsBox.Margin = new Padding(22, 4, 0, 4);
            column.Controls.Add(statsBox);

            return column;
        }

        static Control Row(CheckBox box, Label title)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.AutoSize = true;
            row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            row.Margin = new Padding(0, 8, 0, 0);
            box.Margin = new Padding(0, 2, 4, 0);
            title.Margin = new Padding(0, 4, 0, 0);
            row.Controls.Add(box);
            row.Controls.Add(title);
            return row;
        }

        static Control Fields(Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.AutoSize = true;
            row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            row.Margin = new Padding(22, 2, 0, 2);
            foreach (Control c in controls)
            {
                c.Margin = c is NumericUpDown
                    ? new Padding(0, 0, 6, 0)
                    : new Padding(0, 4, 8, 0);
                row.Controls.Add(c);
            }
            return row;
        }

        // -- right column: the review list -------------------------------------

        Control BuildRightColumn()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.Padding = new Padding(10, 14, 18, 14);
            p.BackColor = Look.Page;

            reviewHeading = Widgets.Label("What will change", Look.Heading, SystemColors.ControlText);
            reviewHeading.Location = new Point(6, 10);
            p.Controls.Add(reviewHeading);

            reviewCount = new Label();
            reviewCount.Font = Look.Base;
            reviewCount.ForeColor = Look.Dim;
            reviewCount.AutoSize = false;
            reviewCount.TextAlign = ContentAlignment.MiddleRight;
            reviewCount.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            reviewCount.Size = new Size(200, 20);
            reviewCount.Location = new Point(p.ClientSize.Width - 220, 12);
            p.Controls.Add(reviewCount);

            reviewList = new Panel();
            reviewList.BackColor = Color.White;
            reviewList.BorderStyle = BorderStyle.FixedSingle;
            reviewList.AutoScroll = true;
            reviewList.Location = new Point(6, 38);
            reviewList.Anchor = AnchorStyles.Top | AnchorStyles.Left
                              | AnchorStyles.Right | AnchorStyles.Bottom;
            reviewList.Size = new Size(p.ClientSize.Width - 30, p.ClientSize.Height - 56);
            reviewList.Resize += delegate { RenderReview(); };
            p.Controls.Add(reviewList);
            return p;
        }

        // -- commit bar ---------------------------------------------------------

        Control BuildCommitBar()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Bottom;
            p.Height = 58;
            p.Padding = new Padding(18, 12, 18, 12);
            p.BackColor = Color.White;

            apply = new Button();
            apply.Dock = DockStyle.Right;
            apply.Width = 220;
            apply.Height = 32;
            apply.Click += delegate { Commit(); };

            commitNote = new Label();
            commitNote.Dock = DockStyle.Fill;
            commitNote.ForeColor = Look.Dim;
            commitNote.TextAlign = ContentAlignment.MiddleLeft;
            commitNote.Text = "APE checks the original bytes at every site before it writes any "
                + "of them. Unchecking a feature and applying puts them back — that is the "
                + "uninstaller.";
            commitNote.Padding = new Padding(0, 0, 12, 0);

            p.Controls.Add(commitNote);
            p.Controls.Add(apply);
            return p;
        }

        void SetTabOrder()
        {
            // Top to bottom through the left column, ending on Apply.
            Control[] order = { hullOn, hullMin, hullMax, dieselOn, rate, bandLo, bandHi, apply };
            for (int i = 0; i < order.Length; i++)
            {
                order[i].TabIndex = i;
                order[i].TabStop = true;
            }
        }

        // -- rendering ----------------------------------------------------------

        void Sync()
        {
            binding = true;
            try
            {
                InstallReport install = session.Install;
                bool supported = install.Supported;
                bool dieselAvailable = supported && install.Available(Manifest.DieselPatchId);

                RenderHeader(install);

                // ---- hull
                hullOn.Enabled = supported;
                hullOn.Checked = desired.HullApplied;
                hullTitle.ForeColor = supported ? SystemColors.ControlText : Look.Dim;
                hullMin.Enabled = hullMax.Enabled = supported && desired.HullApplied;
                hullMin.Value = Clamp(desired.HullMin, hullMin);
                hullMax.Value = Clamp(desired.HullMax, hullMax);
                Widgets.SetWrappedText(hullState, desired.HullApplied
                    ? "APE is applied. Unchecking it and applying puts h.sie's own 5–20% roll back."
                    : "Not applied — h.sie's own 5–20% roll is untouched.",
                    hullState.Width);
                string hullProblem =
                    !desired.HullValid ? "The minimum must not be above the maximum."
                    : desired.HullApplied && desired.HullRepairsNothing
                        ? "The crew repairs nothing, but APE stays applied — that is not the "
                          + "same as unchecking."
                    : "";
                Widgets.SetWrappedText(hullNote, hullProblem, hullNote.Width);
                hullNote.Visible = hullProblem.Length > 0;

                // ---- diesel
                dieselOn.Enabled = dieselAvailable;
                dieselOn.Checked = desired.DieselApplied && dieselAvailable;
                dieselTitle.ForeColor = dieselAvailable ? SystemColors.ControlText : Look.Dim;

                bool fieldsLive = dieselAvailable && desired.DieselApplied;
                rate.Enabled = bandLo.Enabled = bandHi.Enabled = fieldsLive;
                rate.Value = Clamp((decimal)desired.Rate, rate);
                bandLo.Value = Clamp((decimal)desired.BandLo, bandLo);
                bandHi.Value = Clamp((decimal)desired.BandHi, bandHi);
                dieselFields.Visible = dieselAvailable;
                statsBox.Visible = dieselAvailable;

                if (!dieselAvailable)
                {
                    // The state a real user meets first: h.sie's own Diesel Damage
                    // switch is off in every surveyed install.
                    Widgets.SetWrappedText(dieselState, supported
                        ? "Unavailable — h.sie's own Diesel Damage switch is off in this install, "
                          + "so the code APE would tune never runs."
                        : "Unavailable — APE does not recognise this install.",
                        dieselState.Width);
                    Widgets.SetWrappedText(dieselState2, supported
                        ? "Turn it on in HsieOptionsSelector, then reopen APE. APE reads h.sie's "
                          + "switches and never writes them."
                        : "", dieselState2.Width);
                    dieselState2.Visible = supported;
                    dieselNote.Visible = false;
                }
                else
                {
                    Widgets.SetWrappedText(dieselState, desired.DieselApplied
                        ? "APE is applied. Unchecking it and applying puts h.sie's own damage "
                          + "model back."
                        : "Not applied — h.sie's own damage model is untouched.",
                        dieselState.Width);
                    dieselState2.Visible = false;

                    string dieselProblem =
                        !desired.BandValid ? "The band needs a low end above 0 and below the high end."
                        : !desired.RateValid ? "The rate must be above 0."
                        : desired.UnderFloat32Epsilon
                            ? "That rate is finer than the game could otherwise resolve. It works "
                              + "only because APE replaces h.sie's probability helper."
                        : "";
                    Widgets.SetWrappedText(dieselNote, dieselProblem, dieselNote.Width);
                    dieselNote.ForeColor = desired.DieselValid ? Look.Dim : Look.Warn;
                    dieselNote.Visible = dieselProblem.Length > 0;

                    RenderStats();
                }

                RenderReview();
                RenderApplyButton();
            }
            finally
            {
                binding = false;
            }
        }

        static decimal Clamp(decimal v, NumericUpDown n)
        {
            return v < n.Minimum ? n.Minimum : (v > n.Maximum ? n.Maximum : v);
        }

        void RenderHeader(InstallReport install)
        {
            header.BackColor = install.Supported ? Color.White : Look.Refused;
            verdict.Text = install.Supported ? "Install verified" : "Install refused";
            verdict.ForeColor = install.Supported ? SystemColors.ControlText : Look.Warn;
            verdictSub.Text = install.Supported
                ? "APE will only write to a build it recognises."
                : "APE writes nothing to a build it does not recognise.";

            rowPath.Text = "path      " + install.InstallPath;
            rowBuild.Text = "build     " + install.BuildName + "   h.sie " + install.HsieVersion
                + "   sha " + install.ShortSha + "…";
            rowExpected.Text = install.Supported
                ? ""
                : "expected  " + install.SupportedBuilds;
            rowExpected.ForeColor = Look.Warn;
            rowExpected.Visible = !install.Supported;
        }

        void RenderStats()
        {
            statsBox.SuspendLayout();
            statsBox.Controls.Clear();
            foreach (ReviewLine line in Review.DieselStats(desired))
            {
                Label l = Widgets.Label(line.Text, Look.Mono, line.Warn ? Look.Warn : Look.Dim);
                l.Margin = new Padding(0, 0, 0, 1);
                statsBox.Controls.Add(l);
            }
            statsBox.ResumeLayout();
        }

        void RenderReview()
        {
            if (reviewList == null) return;
            WritePlan plan = session.Plan(desired);
            Review review = Review.Build(session.Install, plan, desired);

            reviewCount.Text = review.FeatureCount == 0
                ? ""
                : Fmt.Int(review.FeatureCount)
                  + (review.FeatureCount == 1 ? " feature" : " features");

            reviewList.SuspendLayout();
            reviewList.Controls.Clear();

            int width = reviewList.ClientSize.Width - 34;
            if (width < 120) width = 120;
            int y = 12;

            if (review.Empty)
            {
                Label l = Widgets.Wrapped(review.NothingText ?? "", Look.Base,
                    review.NothingIsWarning ? Look.Warn : Look.Dim, width);
                l.Location = new Point(16, y);
                reviewList.Controls.Add(l);
            }
            else
            {
                foreach (ReviewGroup group in review.Groups)
                {
                    Label title = Widgets.Wrapped(group.Title, Look.Bold,
                        SystemColors.ControlText, width);
                    title.Location = new Point(16, y);
                    reviewList.Controls.Add(title);
                    y += title.Height + 6;

                    foreach (ReviewLine line in group.Lines)
                    {
                        Label l = Widgets.Wrapped("•  " + line.Text, Look.Base,
                            line.Warn ? Look.Warn : SystemColors.ControlText, width);
                        l.Location = new Point(16, y);
                        reviewList.Controls.Add(l);
                        y += l.Height + 4;
                    }
                    y += 12;
                }
                Label foot = Widgets.Wrapped(review.Footnote, Look.Base, Look.Dim, width);
                foot.Location = new Point(16, y);
                reviewList.Controls.Add(foot);
            }
            reviewList.ResumeLayout();
        }

        void RenderApplyButton()
        {
            bool valid = (!desired.HullApplied || desired.HullValid)
                      && (!desired.DieselApplied || desired.DieselValid);
            bool anything = !session.Plan(desired).Empty;

            apply.Enabled = session.Install.Supported && valid && anything;
            apply.Text = !session.Install.Supported ? "Nothing to apply"
                       : !valid ? "Fix the values above"
                       : anything ? "Apply changes"
                       : "Nothing to apply";
        }

        // -- committing ---------------------------------------------------------

        void Commit()
        {
            WriteReport report;
            try
            {
                report = session.Apply(desired);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "APE could not apply your changes",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!report.Ok)
            {
                MessageBox.Show(this, report.Message, "APE wrote nothing",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Reopen();
                return;
            }

            MessageBox.Show(this, Confirmation(report), "Applied",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Reopen();
        }

        static string Confirmation(WriteReport report)
        {
            if (report.SitesWritten.Count == 0)
                return "Nothing needed changing — your install already holds these settings.";

            List<string> lines = new List<string>();
            lines.Add(report.Message);
            lines.Add("");
            foreach (string site in report.SitesWritten) lines.Add("  " + site);
            return string.Join(Environment.NewLine, lines.ToArray());
        }

        /// <summary>Re-read the install so the window keeps describing it, and
        /// carry the player's own choices forward rather than resetting them.</summary>
        void Reopen()
        {
            session = PatchSession.Open(session.Install.InstallPath, session.Manifest);
            desired = session.Current.Clone();
            Sync();
        }
    }
}
