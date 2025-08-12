namespace DCL.DebugUtilities
{
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


