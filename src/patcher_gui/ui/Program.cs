// APE Patch Extensions -- entry point.
//
// One exe, dropped in the SH3 game root.  It finds the install by looking at its
// own location, so the player never has to tell it where the game is.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ApePatcher.Ui
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Pin the culture before a single control exists.  NumericUpDown
            // formats and parses its text through the *current* culture, so on a
            // comma-decimal machine a player would otherwise see and type "0,55"
            // while the model reasoned in "0.55" -- and, far worse, hsie.ini would
            // be written as "4,1667e-8", which h.sie's parser cannot read.  The
            // mod would go inert with no error and no in-game symptom.
            //
            // APE ships in English only; there is no localisation to preserve.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool selfTest = false;
            string dir = null;
            foreach (string a in args)
            {
                if (a == "--self-test") selfTest = true;
                else if (Directory.Exists(a)) dir = a;
            }
            if (dir == null) dir = PatchSession.OwnDirectory();

            if (selfTest)
            {
                Environment.Exit(SelfTest(dir));
                return;
            }

            try
            {
                Application.Run(new MainForm(PatchSession.Open(dir)));
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "APE could not start",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Build the window and lay it out once, without showing it.
        /// The WinForms layer has no automated tests of its own -- it deliberately
        /// holds no logic to test -- but "does it construct, lay out and paint
        /// against a real install" is worth being able to answer from a build
        /// script rather than by eye.
        ///
        /// Exit 0 built and the install was recognised, 2 built but the install
        /// was refused, 1 it threw.</summary>
        static int SelfTest(string dir)
        {
            try
            {
                PatchSession session = PatchSession.Open(dir);
                using (MainForm form = new MainForm(session))
                {
                    form.CreateControl();
                    form.PerformLayout();
                    // Force a paint pass, so a broken custom-drawn control fails
                    // here rather than in front of a player.
                    using (System.Drawing.Bitmap bmp =
                           new System.Drawing.Bitmap(form.Width, form.Height))
                        form.DrawToBitmap(bmp, new System.Drawing.Rectangle(
                            0, 0, form.Width, form.Height));
                }
                return session.Install.Supported ? 0 : 2;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "APE self-test failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
