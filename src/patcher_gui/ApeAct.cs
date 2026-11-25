// Depth stress's ape.act store (ADR-0006, depth-pressure-stress ticket 22).
//
// The C++ build's ape.act is embedded in the patcher as a template. Enabling
// depth stress copies it into the game root with the player's numbers written
// into its constants struct; disabling deletes it. This file holds the struct
// arithmetic only -- checking the marker, writing and reading the fields --
// and PatchSession does every file operation.

using System;
using System.IO;
using System.Reflection;

namespace ApePatcher
{
    public static class DepthStress
    {
        public const string PatchId = "depth-stress";
        public const string PatchName = "Depth stress";
        public const string FileName = "ape.act";
        public const string TemplateResource = "ape.act";

        // No sh3.exe change is needed to load ape.act: stock sh3.exe LoadLibrary's
        // every *.act in the game folder at startup (ADR-0002's 2026-11-25 note).

        // Tooltips (user stories 31, 32, 37). Here rather than in the form so the
        // wording the stories ask for is held by test.
        public const string LaunchNote =
            "Depth-stress settings take effect at the next game launch, not in a game "
            + "that is already running.";

        public const string Tier2DepthTip =
            "Depth stress begins at 100 m for every class. Tier 2, the harsher tier, begins "
            + "at this depth." + "\n"
            + "A class whose crush depth is shallower than this depth goes straight from "
            + "tier 1 to tier 3 (the IIB and XXIII). Below crush depth, tier 2's values keep "
            + "applying." + "\n" + LaunchNote;

        public const string LeakageTip =
            "Water rising in the electric engine room, as a percentage of the compartment "
            + "per game-minute. 0 turns leakage off." + "\n"
            + "The electric engine room must be crewed for its leakage to be pumped. An "
            + "uncrewed one keeps filling, to 80% at most." + "\n" + LaunchNote;

        public const string FloodThresholdTip =
            "How full the electric engine room may get, in percent, before running motors "
            + "start taking damage. This applies at any depth, whatever flooded the room."
            + "\n" + "Ordering stop protects the motors; so does pumping the room below this "
            + "level." + "\n" + LaunchNote;

        public const string MotorDamageTip =
            "How much of each electric motor's health is lost per game-second while the "
            + "room is at or above the flooding threshold and the motors are ordered to run "
            + "(ahead or astern). 0 turns motor damage off." + "\n"
            + "Ordering stop protects the motors. Motor damage never takes a motor past 90%, "
            + "so the crew can always repair it." + "\n" + LaunchNote;

        public const string HullMultTip =
            "Whatever hull damage the boat takes in this tier is multiplied by this. 1.0 "
            + "turns it off. Crush damage itself is never multiplied." + "\n" + LaunchNote;

        /// <summary>The ape.act the patcher was built with, or null if it was
        /// built without one (then depth stress is unavailable).</summary>
        public static byte[] EmbeddedTemplate()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(TemplateResource))
            {
                if (s == null) return null;
                byte[] b = new byte[s.Length];
                int at = 0;
                while (at < b.Length)
                {
                    int n = s.Read(b, at, b.Length - at);
                    if (n <= 0) break;
                    at += n;
                }
                return b;
            }
        }

        /// <summary>Null when the struct's marker and version are where the
        /// manifest says, in this image; otherwise why not.</summary>
        public static string Check(byte[] image, ApeActStruct s)
        {
            if (s == null) return "the manifest records no depth-stress struct";
            if (image == null) return "there is no ape.act template";
            if (s.Offset < 0 || s.Offset + s.Size > image.Length)
                return "the struct's offset falls outside ape.act";
            for (int i = 0; i < s.Magic.Length; i++)
                if (image[s.Offset + i] != s.Magic[i])
                    return "ape.act does not hold the depth-stress marker at the manifest's offset";
            if (BitConverter.ToUInt32(image, s.Offset + s.Magic.Length) != s.Version)
                return "ape.act's depth-stress struct is not version " + Fmt.Int((int)s.Version);
            return null;
        }

        /// <summary>A copy of the template with the desired values written into
        /// the struct, in runtime units. Throws if the marker doesn't check out.</summary>
        public static byte[] Write(byte[] template, ApeActStruct s, PatchState d)
        {
            string problem = Check(template, s);
            if (problem != null) throw new PatchException(problem);
            byte[] image = (byte[])template.Clone();
            Put(image, s, "tier2_depth_m", d.Tier2Depth);
            Put(image, s, "flood_threshold", PatchState.FloodFraction(d.FloodThreshold));
            Put(image, s, "motor_damage_per_s", PatchState.MotorDamagePerSecond(d.MotorDamage));
            Put(image, s, "t1_seep_per_s", PatchState.SeepPerSecond(d.Leakage1));
            Put(image, s, "t1_hull_mult", d.HullMult1);
            Put(image, s, "t2_seep_per_s", PatchState.SeepPerSecond(d.Leakage2));
            Put(image, s, "t2_hull_mult", d.HullMult2);
            return image;
        }

        /// <summary>Reads the struct back into the model's game-term fields.
        /// False, leaving the model alone, if the marker doesn't check out.</summary>
        public static bool Read(byte[] image, ApeActStruct s, PatchState into)
        {
            if (Check(image, s) != null) return false;
            into.Tier2Depth = Math.Round(Get(image, s, "tier2_depth_m"), 2);
            into.Leakage1 = Math.Round(PatchState.LeakageFromSeep(Get(image, s, "t1_seep_per_s")), 2);
            into.Leakage2 = Math.Round(PatchState.LeakageFromSeep(Get(image, s, "t2_seep_per_s")), 2);
            into.FloodThreshold = Math.Round(PatchState.ThresholdFromFraction(Get(image, s, "flood_threshold")), 2);
            into.MotorDamage = Math.Round(PatchState.MotorDamageFromPerSecond(Get(image, s, "motor_damage_per_s")), 2);
            into.HullMult1 = Math.Round(Get(image, s, "t1_hull_mult"), 2);
            into.HullMult2 = Math.Round(Get(image, s, "t2_hull_mult"), 2);
            return true;
        }

        static void Put(byte[] image, ApeActStruct s, string field, double value)
        {
            BitConverter.GetBytes((float)value).CopyTo(image, s.Offset + s.FieldAt(field));
        }

        static double Get(byte[] image, ApeActStruct s, string field)
        {
            return BitConverter.ToSingle(image, s.Offset + s.FieldAt(field));
        }
    }
}
