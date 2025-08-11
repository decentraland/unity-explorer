using DCL.DebugUtilities.UIBindings;

namespace DCL.DebugUtilities
{
    /// <summary>
    ///     Definition for a custom UI Toolkit banner that shows an Average FPS summary.
    ///     The binding value is expected to be the average frame time in nanoseconds.
    /// </summary>
    public class AverageFpsBannerDef : IDebugElementDef
    {
        public readonly ElementBinding<float> AvgFrameTimeNsBinding;

        /// <summary>
        ///     If FPS is below this value, apply Warning visuals.
        /// </summary>
        public readonly float WarningFpsThreshold;

        /// <summary>
        ///     If FPS is below this value, apply Error visuals.
        /// </summary>
        public readonly float ErrorFpsThreshold;

        public AverageFpsBannerDef(ElementBinding<float> avgFrameTimeNsBinding, float warningFpsThreshold = 30f, float errorFpsThreshold = 20f)
        {
            AvgFrameTimeNsBinding = avgFrameTimeNsBinding;
            WarningFpsThreshold = warningFpsThreshold;
            ErrorFpsThreshold = errorFpsThreshold;
        }
    }
}
