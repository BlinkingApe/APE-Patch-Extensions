// The one window: prototype variant C, folded into the real tool.
//
// An install-verification header across the top, the features down the left,
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
        Label rowPath, rowBuild;

        // hull
        CheckBox hullOn;
        Label hullTitle, hullState, hullNote;
        NumericUpDown hullMin, hullMax;

        // diesel
        CheckBox dieselOn;
        Label dieselTitle, dieselState, dieselState2, dieselNote;
        NumericUpDown rate, bandLo, bandHi;
        Label rateUnitLabel;
        FlowLayoutPanel statsBox;
        Panel dieselFields;

        // crush time (depth-pressure-stress ticket 16)
        CheckBox crushOn;
        Label crushTitle, crushState, crushNote;
        NumericUpDown crushTime;
        Control crushFields;
        ToolTip tips;

        // depth stress (depth-pressure-stress ticket 23)
        CheckBox depthOn;
        Label depthTitle, depthState, depthNote;
        Control depthFields;
        List<DepthField> depthInputs = new List<DepthField>();

        class DepthField
        {
            public NumericUpDown Box;
            public Func<double> Get;
        }

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
            ClientSize = new Size(1010, 760);
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
            header.Controls.Add(rowPath);
            header.Controls.Add(rowBuild);
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

        // -- left column: the features -----------------------------------------

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

            Site hullFloor = SiteOf(Manifest.HullPatchId, "hull-roll-floor");
            Site hullWidth = SiteOf(Manifest.HullPatchId, "hull-roll-width");
            hullMin = Widgets.Num(desired.HullMin, (decimal)hullFloor.Min, (decimal)hullFloor.Max, 0, 1, 62);
            hullMax = Widgets.Num(desired.HullMax, (decimal)hullWidth.Min, (decimal)hullWidth.Max, 0, 1, 62);
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

            rate = Widgets.RateSpinner((decimal)desired.Rate, 0.1m, (decimal)PatchState.RateCeiling, 72);
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
            rateUnitLabel = Widgets.Label("events per 10,000 hours of running", Look.Base, Look.Dim);
            stack.Controls.Add(Fields(new Control[] { rate, rateUnitLabel }));
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

            // ---- crush time
            crushOn = new CheckBox();
            crushOn.AutoSize = true;
            crushOn.Text = "";
            crushTitle = Widgets.Label(CrushTime.PatchName, Look.Bold, SystemColors.ControlText);
            column.Controls.Add(Row(crushOn, crushTitle));
            crushOn.CheckedChanged += delegate { if (!binding) { desired.CrushApplied = crushOn.Checked; Sync(); } };

            crushState = Widgets.Wrapped("", Look.Base, Look.Dim, inner);
            crushState.Margin = new Padding(22, 2, 0, 4);
            column.Controls.Add(crushState);

            crushTime = Widgets.Num(desired.CrushTime, PatchState.CrushTimeMin,
                PatchState.CrushTimeMax, 0, 1, 72);
            crushTime.ValueChanged += delegate { if (!binding) { desired.CrushTime = (int)crushTime.Value; Sync(); } };
            Label crushLabel = Widgets.Label("Crush time (seconds)", Look.Base, Look.Dim);
            crushFields = Fields(new Control[] { crushLabel, crushTime });
            column.Controls.Add(crushFields);

            tips = new ToolTip();
            string tip = "Roughly how long a U-boat at full hull lasts below crush depth, on the "
                + "same scale for every class." + Environment.NewLine
                + "Crush damage grows the deeper the boat goes and the weaker its hull gets: just "
                + "past crush depth a boat lasts a little longer than this, at its rated crush "
                + "depth about a third as long." + Environment.NewLine
                + "Crush depth begins at 70% of the rated crush depth at full hull, and gets "
                + "shallower as the hull is damaged." + Environment.NewLine
                + "APE writes each class's crush speed as its hull hit points divided by this time."
                + Environment.NewLine
                + "The game reads it when a mission loads, so a change takes effect from the next "
                + "mission you start or load.";
            tips.SetToolTip(crushTime, tip);
            tips.SetToolTip(crushLabel, tip);

            crushNote = Widgets.Wrapped("", Look.Base, Look.Warn, inner);
            crushNote.Margin = new Padding(22, 2, 0, 12);
            column.Controls.Add(crushNote);

            // ---- depth stress
            depthOn = new CheckBox();
            depthOn.AutoSize = true;
            depthOn.Text = "";
            depthTitle = Widgets.Label(DepthStress.PatchName, Look.Bold, SystemColors.ControlText);
            column.Controls.Add(Row(depthOn, depthTitle));
            depthOn.CheckedChanged += delegate { if (!binding) { desired.DepthApplied = depthOn.Checked; Sync(); } };

            depthState = Widgets.Wrapped("", Look.Base, Look.Dim, inner);
            depthState.Margin = new Padding(22, 2, 0, 4);
            column.Controls.Add(depthState);

            FlowLayoutPanel depthStack = new FlowLayoutPanel();
            depthStack.FlowDirection = FlowDirection.TopDown;
            depthStack.WrapContents = false;
            depthStack.AutoSize = true;
            depthStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            depthStack.Margin = new Padding(0, 0, 0, 0);
            depthFields = depthStack;
            column.Controls.Add(depthStack);

            AddDepth(depthStack, "Tier-2 depth", "m", PatchState.Tier2DepthMin, PatchState.Tier2DepthMax,
                0, 1, DepthStress.Tier2DepthTip, delegate { return desired.Tier2Depth; },
                delegate(double v) { desired.Tier2Depth = v; });
            depthStack.Controls.Add(Widgets.Label("Tier 1 — from 100 m", Look.Bold, Look.Dim));
            AddDepth(depthStack, "Leakage", "% per game-minute", PatchState.LeakageMin, PatchState.LeakageMax,
                1, 0.5m, DepthStress.LeakageTip, delegate { return desired.Leakage1; },
                delegate(double v) { desired.Leakage1 = v; });
            AddDepth(depthStack, "Hull multiplier", "×", (decimal)PatchState.HullMultMin,
                (decimal)PatchState.HullMultMax, 2, 0.05m, DepthStress.HullMultTip,
                delegate { return desired.HullMult1; }, delegate(double v) { desired.HullMult1 = v; });
            depthStack.Controls.Add(Widgets.Label("Tier 2 — from the tier-2 depth", Look.Bold, Look.Dim));
            AddDepth(depthStack, "Leakage", "% per game-minute", PatchState.LeakageMin, PatchState.LeakageMax,
                1, 0.5m, DepthStress.LeakageTip, delegate { return desired.Leakage2; },
                delegate(double v) { desired.Leakage2 = v; });
            AddDepth(depthStack, "Hull multiplier", "×", (decimal)PatchState.HullMultMin,
                (decimal)PatchState.HullMultMax, 2, 0.05m, DepthStress.HullMultTip,
                delegate { return desired.HullMult2; }, delegate(double v) { desired.HullMult2 = v; });
            depthStack.Controls.Add(Widgets.Label("Motor flooding — at any depth", Look.Bold, Look.Dim));
            AddDepth(depthStack, "Flooding threshold", "% flood", PatchState.FloodThresholdMin,
                PatchState.FloodThresholdMax, 0, 5, DepthStress.FloodThresholdTip,
                delegate { return desired.FloodThreshold; }, delegate(double v) { desired.FloodThreshold = v; });
            AddDepth(depthStack, "Motor damage", "% per game-second", PatchState.MotorDamageMin,
                PatchState.MotorDamageMax, 1, 0.5m, DepthStress.MotorDamageTip,
                delegate { return desired.MotorDamage; }, delegate(double v) { desired.MotorDamage = v; });

            depthNote = Widgets.Wrapped("", Look.Base, Look.Warn, inner);
            depthNote.Margin = new Padding(22, 2, 0, 12);
            column.Controls.Add(depthNote);

            return column;
        }

        void AddDepth(FlowLayoutPanel into, string label, string unit, double min, double max,
            int decimals, decimal step, string tip, Func<double> get, Action<double> set)
        {
            AddDepth(into, label, unit, (decimal)min, (decimal)max, decimals, step, tip, get, set);
        }

        void AddDepth(FlowLayoutPanel into, string label, string unit, decimal min, decimal max,
            int decimals, decimal step, string tip, Func<double> get, Action<double> set)
        {
            NumericUpDown box = Widgets.Num((decimal)get(), min, max, decimals, step, 72);
            box.ValueChanged += delegate { if (!binding) { set((double)box.Value); Sync(); } };
            Label name = Widgets.Label(label, Look.Base, Look.Dim);
            name.AutoSize = false;
            name.Width = 110;
            Label units = Widgets.Label(unit, Look.Base, Look.Dim);
            tips.SetToolTip(box, tip);
            tips.SetToolTip(name, tip);
            into.Controls.Add(Fields(new Control[] { name, box, units }));
            DepthField f = new DepthField();
            f.Box = box;
            f.Get = get;
            depthInputs.Add(f);
        }

        Site SiteOf(string patchId, string siteId)
        {
            foreach (Site s in session.Manifest.Patch(patchId).Sites)
                if (s.Id == siteId) return s;
            throw new PatchException("Manifest has no site '" + siteId + "'.");
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
            List<Control> order = new List<Control>(new Control[] { hullOn, hullMin, hullMax,
                dieselOn, rate, bandLo, bandHi, crushOn, crushTime, depthOn });
            foreach (DepthField f in depthInputs) order.Add(f.Box);
            order.Add(apply);
            for (int i = 0; i < order.Count; i++)
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

                    rateUnitLabel.Text = desired.IsTestRate
                        ? "events per 10,000 hours — test range, not a play value"
                        : "events per 10,000 hours of running";
                    rateUnitLabel.ForeColor = desired.IsTestRate ? Look.Warn : Look.Dim;

                    string dieselProblem =
                        !desired.BandValid ? "The band needs a low end above 0 and below the high end."
                        : !desired.RateValid ? "The rate must be above 0 and at or below "
                              + Fmt.Int((int)PatchState.RateCeiling) + "."
                        : desired.UnderFloat32Epsilon
                            ? "That rate is finer than the game could otherwise resolve. It works "
                              + "only because APE replaces h.sie's probability helper."
                        : desired.IsTestRate
                            ? "That rate is a test value, far above anything a real patrol would "
                              + "produce -- it exists to run the blackbox procedure through APE, "
                              + "not to ship with."
                        : "";
                    Widgets.SetWrappedText(dieselNote, dieselProblem, dieselNote.Width);
                    dieselNote.ForeColor = !desired.DieselValid ? Look.Warn
                        : desired.IsTestRate ? Look.Warn
                        : Look.Dim;
                    dieselNote.Visible = dieselProblem.Length > 0;

                    RenderStats();
                }

                RenderCrush(install, supported);
                RenderDepth(install, supported);
                RenderReview();
                RenderApplyButton();
            }
            finally
            {
                binding = false;
            }
        }

        void RenderCrush(InstallReport install, bool supported)
        {
            bool available = supported && install.Available(CrushTime.PatchId);
            bool applied = supported && install.Applied(CrushTime.PatchId);
            bool usable = available || applied;

            crushOn.Enabled = usable;
            crushOn.Checked = desired.CrushApplied && usable;
            crushTitle.ForeColor = usable ? SystemColors.ControlText : Look.Dim;
            crushTime.Enabled = usable && desired.CrushApplied;
            crushTime.Value = Clamp(desired.CrushTime, crushTime);
            crushFields.Visible = usable;

            int classes = 0;
            List<string> leftAlone = new List<string>();
            foreach (UBoatZon b in install.UBoats)
            {
                string why = Review.LeftAlone(b);
                if (why == null) classes++;
                else leftAlone.Add(why);
            }

            string state =
                !supported ? "Unavailable — APE does not recognise this install."
                : !usable ? "Unavailable — APE found no playable U-boat .zon file under "
                            + "data/Submarine that it can write."
                : desired.CrushApplied && applied
                    ? "APE is applied to " + Fmt.Int(classes) + " U-boat classes. Unchecking it "
                      + "and applying puts each class's own crush speed back."
                : desired.CrushApplied
                    ? "Sets the crush speed of " + Fmt.Int(classes) + " playable U-boat classes."
                : "Not applied — each U-boat keeps its own crush speed.";
            Widgets.SetWrappedText(crushState, state, crushState.Width);

            string problem = !desired.CrushTimeValid
                ? "The crush time must be from " + Fmt.Int(PatchState.CrushTimeMin) + " to "
                  + Fmt.Int(PatchState.CrushTimeMax) + " seconds."
                : string.Join(" ", leftAlone.ToArray());
            Widgets.SetWrappedText(crushNote, problem, crushNote.Width);
            crushNote.Visible = problem.Length > 0;
        }

        void RenderDepth(InstallReport install, bool supported)
        {
            bool available = supported && install.Available(DepthStress.PatchId);
            bool deployed = supported && install.Applied(DepthStress.PatchId);
            bool loads = DepthStress.LoadCallSiteExists;

            // Until the load call site exists the section is shown disabled; the one
            // thing still allowed is taking out an ape.act that is already there.
            bool usable = available && loads;
            bool canRemove = deployed && !loads;

            depthOn.Enabled = usable || (canRemove && desired.DepthApplied);
            depthOn.Checked = desired.DepthApplied && (usable || canRemove);
            depthTitle.ForeColor = (usable || canRemove) ? SystemColors.ControlText : Look.Dim;
            bool fieldsLive = usable && desired.DepthApplied;
            foreach (DepthField f in depthInputs)
            {
                f.Box.Enabled = fieldsLive;
                f.Box.Value = Clamp((decimal)f.Get(), f.Box);
            }
            depthFields.Visible = supported;

            string state =
                !supported ? "Unavailable — APE does not recognise this install."
                : !loads ? (deployed ? DepthStress.NotLoadedButDeployed : DepthStress.NotLoadedYet)
                : !available ? "Unavailable — this build of APE carries no depth-stress module."
                : desired.DepthApplied && deployed
                    ? "APE is applied. Unchecking it and applying removes depth stress."
                : desired.DepthApplied
                    ? "Depth stress begins at 100 m. " + DepthStress.LaunchNote
                : "Not applied — depth costs a boat nothing until crush depth.";
            Widgets.SetWrappedText(depthState, state, depthState.Width);

            string problem = desired.DepthApplied && !desired.DepthStressValid
                ? "A depth-stress value is out of range. Hover a field to see what it allows."
                : "";
            Widgets.SetWrappedText(depthNote, problem, depthNote.Width);
            depthNote.Visible = problem.Length > 0;
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
                ? "APE will only write when hsie has been installed."
                : "APE won't write without hsie installed.";

            rowPath.Text = "path      " + install.InstallPath;
            rowBuild.Text = "h.sie     " + install.HsieVersion
                + "   sha " + install.ShortSha + "…" + "   APE " + ApeVersion.Current;
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
                      && (!desired.DieselApplied || desired.DieselValid)
                      && (!desired.CrushApplied || desired.CrushTimeValid)
                      && (!desired.DepthApplied || desired.DepthStressValid);
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

            if (!report.Ok && report.ForeignWrite)
            {
                DialogResult choice = MessageBox.Show(this, report.Message + Environment.NewLine
                    + Environment.NewLine + "Apply anyway, overwriting your edit with APE's value?",
                    "hsie.ini holds a value APE did not write",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (choice == DialogResult.Yes)
                {
                    try
                    {
                        report = session.Apply(desired, true);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(this, e.Message, "APE could not apply your changes",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    Reopen();
                    return;
                }
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
