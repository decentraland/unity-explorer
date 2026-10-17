using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for a custom UI Toolkit banner that shows an Average FPS summary
    /// </summary>
    public class AverageFpsBannerDef : IDebugElementDef
    {
        public readonly ElementBinding<AverageFpsBannerData> AvgDisplayBinding;

        /// <summary>
        ///     If FPS is below this value, change FPS color
        /// </summary>
        public readonly float NormalFpsThreshold;

        /// <summary>
        ///     If FPS is below this value, change FPS color
        /// </summary>
        public readonly float BadFpsThreshold;

        public AverageFpsBannerDef(
            ElementBinding<AverageFpsBannerData> avgDisplayBinding,
            float normalFpsThreshold = 45f,
            float badFpsThreshold = 30f)
        {
            NormalFpsThreshold = normalFpsThreshold;
            BadFpsThreshold = badFpsThreshold;
            AvgDisplayBinding = avgDisplayBinding;
        }
    }

    /// <summary>
    ///     DTO for precomputed Average FPS display values to avoid UI-side calculations.
    /// </summary>
    public readonly struct AverageFpsBannerData
    {
        public readonly float Fps;
        public readonly float Ms;

        public AverageFpsBannerData(float fps, float ms)
        {
            Fps = fps;
            Ms = ms;
        }
    }
}
