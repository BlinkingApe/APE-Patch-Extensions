// The desired-state model's arithmetic.  This is the model PatchSession hands
// out and takes back, so testing it here also covers everything the UI renders --
// no probability, threshold or encoding rule lives in the WinForms layer.

using System.Globalization;
using ApePatcher;

namespace ApePatcher.Tests
{
    static class PatchStateTests
    {
        static PatchState Default()
        {
            PatchState s = new PatchState();
            s.HullApplied = true;
            s.HullMin = 5;
            s.HullMax = 20;
            s.DieselApplied = true;
            s.Rate = 1.5;
            s.BandLo = 0.15;
            s.BandHi = 0.55;
            return s;
        }

        // -- the rate, and the value it becomes in hsie.ini --------------------

        public static void Test_PdsoIsRatePerTenThousandHours()
        {
            PatchState s = Default();
            // 1.5 events / 10,000 h = 1.5 / (10,000 x 3600) per game-second
            Check.Close(4.1667e-8, s.Pdso, 1e-12, "p_dso at the default rate");
        }

        public static void Test_ThresholdIsDerivedFromPdso()
        {
            // Val10 = p_dso x 0.1^0.01 = p_dso x 0.977237, the idle cutoff that
            // makes h.sie's own threshold test mean "telegraph > 0.1"
            // (umbrella ticket 12, finding A's fix).
            PatchState s = Default();
            Check.Close(s.Pdso * 0.977237, s.Threshold, 1e-18, "Val10 is derived");
            Check.True(s.Threshold < s.Pdso, "the threshold sits below p_dso, or the mod is inert");
        }

        public static void Test_RateRoundTripsThroughPdso()
        {
            PatchState s = Default();
            s.Rate = 12.7;
            PatchState back = new PatchState();
            back.SetRateFromPdso(s.Pdso);
            Check.Close(12.7, back.Rate, 1e-9, "rate survives the trip through p_dso");
        }

        // -- the float32 lattice floor ----------------------------------------

        public static void Test_Float32EpsilonFlagAtTheBoundary()
        {
            // 2.98e-8 is half a float32 ULP below 1.0: under it, h.sie's stock
            // helper would snap p to zero.  APE's rewrite is what makes rates
            // below it work at all, and the UI has to say so.
            PatchState s = Default();

            s.SetRateFromPdso(2.98e-8 * 1.01);
            Check.False(s.UnderFloat32Epsilon, "just above the lattice floor");

            s.SetRateFromPdso(2.98e-8 * 0.99);
            Check.True(s.UnderFloat32Epsilon, "just below the lattice floor");
        }

        // -- the damage band ---------------------------------------------------

        public static void Test_MeanDamageIsTheBandMidpoint()
        {
            PatchState s = Default();
            Check.Close(0.35, s.MeanDamage, 1e-12, "mean damage at 0.15-0.55");
        }

        public static void Test_OneHitNeverDestroysBelowOne()
        {
            PatchState s = Default();
            Check.True(s.OneHitNeverDestroys, "hi 0.55 cannot reach 1.0 in one hit");
            s.BandHi = 0.99;
            Check.True(s.OneHitNeverDestroys, "hi 0.99 still cannot reach 1.0 in one hit");
        }

        public static void Test_DestructionThresholdAtHalf()
        {
            // The threshold, not the percentage, is the load-bearing fact:
            // destruction is possible if and only if hi > 0.50.
            PatchState s = Default();

            s.BandHi = 0.49;
            Check.False(s.TwoHitsCanDestroy, "hi 0.49 -- two hits cannot reach 1.0");

            s.BandHi = 0.50;
            Check.False(s.TwoHitsCanDestroy, "hi exactly 0.50 -- only a measure-zero pair reaches 1.0");

            s.BandHi = 0.51;
            Check.True(s.TwoHitsCanDestroy, "hi 0.51 -- two hits can reach 1.0");
        }

        public static void Test_TwoHitDestroyPercentageAtTheDefaultBand()
        {
            // P(X + Y >= 1.0) for X, Y ~ U(0.15, 0.55) = 3.125%, which is the
            // 3.1% umbrella ticket 04 records for this band.
            PatchState s = Default();
            Check.Close(3.125, s.PercentTwoHitsDestroy, 1e-9, "the ticket-04 figure");
        }

        public static void Test_TwoHitDestroyPercentageIsZeroBelowTheThreshold()
        {
            PatchState s = Default();
            s.BandHi = 0.49;
            Check.Close(0.0, s.PercentTwoHitsDestroy, 1e-12, "no chance below the threshold");
            s.BandHi = 0.50;
            Check.Close(0.0, s.PercentTwoHitsDestroy, 1e-12, "no chance at exactly the threshold");
        }

        // -- the rate ceiling (ticket 01 stories 1-3) --------------------------

        public static void Test_TheRateCeilingReachesTheBlackboxProcedure()
        {
            // The blackbox test's p_dso = 0.01/s is 360,000 events per 10,000
            // hours exactly -- the documented procedure has to be reachable at
            // the top of the range and nothing beyond it is needed.
            PatchState s = Default();
            s.Rate = 360000.0;
            Check.True(s.RateValid, "the ceiling itself is accepted");
            Check.Close(0.01, s.Pdso, 1e-15, "and it really is p_dso = 0.01/s");

            s.Rate = 360000.01;
            Check.False(s.RateValid, "just above the ceiling is refused");
        }

        public static void Test_RatesFarAboveRealisticPlayAreMarkedAsTestValues()
        {
            PatchState s = Default();
            s.Rate = 4.0;
            Check.False(s.IsTestRate, "an ordinary play rate is not a test value");

            s.Rate = 1000.0;
            Check.False(s.IsTestRate, "the old play-range ceiling itself is not a test value");

            s.Rate = 1000.01;
            Check.True(s.IsTestRate, "just above it is marked as a test value");

            s.Rate = 360000.0;
            Check.True(s.IsTestRate, "and so is the new technical ceiling");
        }

        // -- validation --------------------------------------------------------

        public static void Test_BandMustBeStrictlyIncreasing()
        {
            PatchState s = Default();
            Check.True(s.BandValid, "0.15 < 0.55 is coherent");
            s.BandLo = 0.55;
            Check.False(s.BandValid, "lo equal to hi is not a range");
            s.BandLo = 0.60;
            Check.False(s.BandValid, "lo above hi is not a range");
        }

        public static void Test_HullMinimumMayNotExceedMaximum()
        {
            PatchState s = Default();
            Check.True(s.HullValid, "5-20 is coherent");
            s.HullMin = 30;
            Check.False(s.HullValid, "30-20 is not a range");
            s.HullMin = 20;
            Check.True(s.HullValid, "a single-valued range is still coherent");
        }

        public static void Test_HullZeroToZeroIsAppliedButInert()
        {
            PatchState s = Default();
            s.HullMin = 0;
            s.HullMax = 0;
            Check.True(s.HullValid, "0-0 is a coherent range");
            Check.True(s.HullRepairsNothing, "0-0 means the crew repairs nothing");
        }

        // -- formatting --------------------------------------------------------

        public static void Test_DerivedStatisticsReadTheSameInEveryCulture()
        {
            // A screenshot has to mean the same thing on a comma-decimal machine.
            PatchState s = Default();
            Check.Equal("0.35", Fmt.F2(s.MeanDamage), "mean damage");
            Check.Equal("3.1", Fmt.F1(s.PercentTwoHitsDestroy), "destroy percentage");
            Check.Equal("4.1667e-08", Fmt.Sci(s.Pdso), "wear rate");
            Check.Equal("1.5", Fmt.F1(s.Rate), "rate");
        }

        public static void Test_CurrentCultureCannotLeakIntoTheseNumbers()
        {
            // Belt and braces: if the current culture is comma-decimal, a bare
            // ToString() would produce "0,35".  Prove the helpers do not.
            PatchState s = Default();
            string mean = Fmt.F2(s.MeanDamage);
            Check.DoesNotContain(",", mean, "no comma decimal separator survives");
            Check.Contains(".", mean, "the invariant separator is used");
            Check.DoesNotContain(",", Fmt.Sci(s.Pdso), "no comma in the wear rate");
        }
    }
}
