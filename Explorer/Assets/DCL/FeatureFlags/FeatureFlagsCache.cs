namespace DCL.FeatureFlags
{
    public class FeatureFlagsCache
    {
        public FeatureFlagsConfiguration Configuration { get; internal set; } = new (FeatureFlagsResultDto.Empty);
    }
}
