using System;

namespace DCL.Shop
{
    /// <summary>
    ///     The price filter's piecewise scale: the first half of the track covers 0 to <see cref="KNEE_CREDITS" /> in
    ///     steps of 10 credits, the second half stretches from there to <see cref="MAX_CREDITS" /> rounded to hundreds.
    /// </summary>
    public static class ShopPriceSliderScale
    {
        public const int MAX_CREDITS = 100_000;
        public const int STEPS = KNEE_STEP * 2;

        private const int KNEE_CREDITS = 900;
        private const int KNEE_STEP = 90;
        private const int LOW_STEP_CREDITS = KNEE_CREDITS / KNEE_STEP;
        private const int HIGH_SPAN_CREDITS = MAX_CREDITS - KNEE_CREDITS;
        private const int HIGH_ROUND_CREDITS = 100;

        public static int StepToCredits(int step)
        {
            step = Math.Clamp(step, 0, STEPS);

            if (step <= KNEE_STEP)
                return step * LOW_STEP_CREDITS;

            double raw = KNEE_CREDITS + ((step - KNEE_STEP) / (double)KNEE_STEP * HIGH_SPAN_CREDITS);
            return (int)Math.Round(raw / HIGH_ROUND_CREDITS, MidpointRounding.AwayFromZero) * HIGH_ROUND_CREDITS;
        }

        public static int CreditsToStep(int credits)
        {
            credits = Math.Clamp(credits, 0, MAX_CREDITS);

            if (credits <= KNEE_CREDITS)
                return (int)Math.Round(credits / (double)LOW_STEP_CREDITS, MidpointRounding.AwayFromZero);

            return KNEE_STEP + (int)Math.Round((credits - KNEE_CREDITS) / (double)HIGH_SPAN_CREDITS * KNEE_STEP, MidpointRounding.AwayFromZero);
        }
    }
}
