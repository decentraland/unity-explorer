namespace DCL.Quality
{
    public enum QualityPresetLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Custom = 3,
    }

    public enum GrassPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    public enum SunShadowQuality
    {
        Low = 1,
        Medium = 2,
        High = 3,
    }

    public enum ShadowQualityLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
    }

    public enum ShadowDistanceLevel
    {
        Short = 0,
        Medium = 1,
        Far = 2,
    }

    public enum MsaaLevel
    {
        Off = 0,
        X2 = 1,
        X4 = 2,
        X8 = 3,
    }

    public static class MsaaLevelExtensions
    {
        public static int ToSampleCount(this MsaaLevel level) =>
            level switch
            {
                MsaaLevel.Off => 0,
                MsaaLevel.X2 => 2,
                MsaaLevel.X4 => 4,
                MsaaLevel.X8 => 8,
                _ => 0,
            };
    }
}
