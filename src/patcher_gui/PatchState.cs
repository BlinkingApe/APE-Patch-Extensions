// The desired-state model: APE's two features, their fields, and every statistic
// derived from them.
//
// No I/O, no WinForms, no manifest.  PatchSession hands one of these out to
// describe what an install currently holds, and takes one back to describe what
// the player wants.  The UI binds controls to it and renders its derived
// properties verbatim -- no probability or threshold arithmetic lives up there.

using System;
using System.Globalization;

namespace ApePatcher
{
    /// <summary>Invariant-culture formatting. Every number APE shows or writes
    /// goes through here; nothing may call ToString() without a culture.</summary>
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string F1(double d) { return d.ToString("0.0", Inv); }
        public static string F2(double d) { return d.ToString("0.00", Inv); }
        public static string Sci(double d) { return d.ToString("0.0000e-00", Inv); }
        public static string Int(int i) { return i.ToString(Inv); }

        public static bool TryParse(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float, Inv, out value);
        }

        public static double Parse(string s)
        {
            return double.Parse(s, NumberStyles.Float, Inv);
        }
    }

    public class PatchState
    {
        // Seconds of game time in 10,000 hours -- the unit the rate field is
        // expressed in, because a raw per-second probability means nothing to a
        // player (umbrella ticket 04).
        public const double SecondsPerTenThousandHours = 10000.0 * 3600.0;

        // Val10 = p_dso x 0.1^0.01.  Because Speed^0.01 is monotonic this makes
        // h.sie's existing threshold test at 0x635129 mean "telegraph > 0.1"
        // exactly -- the idle cutoff, with no code patch (umbrella ticket 12).
        public const double IdleCutoffFactor = 0.977237;

        // Half a float32 ULP below 1.0.  h.sie's stock helper rounds (1 - p) to
        // float32, so below this p is snapped to zero and the mod goes silently
        // inert under time compression.  APE's rewrite of 0x635818 removes the
        // lattice, which is the only reason rates under it are offered at all.
        public const double Float32EpsilonFloor = 2.98e-8;

        // Above this maximum, two hits on the same engine can sum to 1.0.
        public const double DestructionThreshold = 0.50;

        public bool HullApplied;
        public int HullMin = 5;
        public int HullMax = 20;

        public bool DieselApplied;
        public double Rate = 1.5;      // events per 10,000 h of diesel running
        public double BandLo = 0.15;   // fraction of one engine, per event
        public double BandHi = 0.55;

        public PatchState Clone()
        {
            return (PatchState)MemberwiseClone();
        }

        // -- hull repair -------------------------------------------------------

        /// <summary>h.sie rolls (rand(0..15) + 5); APE narrows the roll's width
        /// and floor. Width is max - min + 1, so min == max gives a fixed value
        /// and 0-0 gives a repair of nothing.</summary>
        public int HullRollWidth { get { return HullMax - HullMin + 1; } }
        public int HullRollFloor { get { return HullMin; } }

        public bool HullValid
        {
            get { return HullMin >= 0 && HullMax >= HullMin && HullMax <= 100; }
        }

        /// <summary>Applied, but set so the crew restores nothing -- which is not
        /// the same as unchecking the feature.</summary>
        public bool HullRepairsNothing { get { return HullMin == 0 && HullMax == 0; } }

        public double HullMean { get { return (HullMin + HullMax) / 2.0; } }

        // -- diesel damage -----------------------------------------------------

        /// <summary>The base probability per game-second, written to Val9.</summary>
        public double Pdso { get { return Rate / SecondsPerTenThousandHours; } }

        /// <summary>The derived idle cutoff, written to Val10 on every write and
        /// never shown or editable.</summary>
        public double Threshold { get { return Pdso * IdleCutoffFactor; } }

        public void SetRateFromPdso(double pdso)
        {
            Rate = pdso * SecondsPerTenThousandHours;
        }

        public bool UnderFloat32Epsilon { get { return Pdso < Float32EpsilonFloor; } }

        public double MeanDamage { get { return (BandLo + BandHi) / 2.0; } }

        public bool BandValid
        {
            get { return 0.0 < BandLo && BandLo < BandHi && BandHi < 1.0; }
        }

        // The rate ceiling a real damage model can express: 360,000 events per
        // 10,000 hours is the blackbox procedure's p_dso = 0.01/s exactly
        // (umbrella ticket 12), and nothing beyond it is needed.
        public const double RateCeiling = 360000.0;

        // The old ceiling, before the blackbox procedure needed more headroom.
        // It was a taste judgement about realistic play, not a model constraint,
        // so it survives as the line between a play-range rate and a test-range
        // one rather than being deleted (ticket 01 stories 1-2).
        public const double RealisticRateCeiling = 1000.0;

        public bool RateValid { get { return Rate > 0.0 && Rate <= RateCeiling; } }

        /// <summary>True once the rate is so far above realistic play that it can
        /// only be a deliberate test value -- the blackbox procedure's territory,
        /// not a shipping setting a player would choose.</summary>
        public bool IsTestRate { get { return Rate > RealisticRateCeiling; } }

        /// <summary>The crew-repairable guarantee the whole band decision rests
        /// on: a single hit cannot reach 1.0 while the maximum is below it.
        /// Destruction is terminal for the patrol (umbrella ticket 12, step 6).</summary>
        public bool OneHitNeverDestroys { get { return BandHi < 1.0; } }

        /// <summary>Two hits on the same engine can sum to 1.0 only above 0.50.
        /// Exactly 0.50 needs both draws to land exactly on the maximum -- a
        /// measure-zero event, deliberately read as "cannot".</summary>
        public bool TwoHitsCanDestroy { get { return BandHi > DestructionThreshold; } }

        /// <summary>P(X + Y >= 1.0) as a percentage, for X, Y ~ U(lo, hi).
        /// Conditional on two hits landing on the same engine with the first
        /// still unrepaired -- never present it as a per-patrol risk.</summary>
        public double PercentTwoHitsDestroy
        {
            get
            {
                if (!TwoHitsCanDestroy) return 0.0;
                double lo = BandLo, hi = BandHi, w = hi - lo, s = 1.0;
                if (w <= 0) return hi * 2 >= s ? 100.0 : 0.0;
                if (s <= 2 * lo) return 100.0;
                if (s >= 2 * hi) return 0.0;
                if (s >= lo + hi) { double t = 2 * hi - s; return 100.0 * (t * t) / (2 * w * w); }
                double u = s - 2 * lo;
                return 100.0 * (1.0 - (u * u) / (2 * w * w));
            }
        }

        public bool DieselValid { get { return BandValid && RateValid; } }
    }
}
