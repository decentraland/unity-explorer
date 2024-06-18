namespace DCL.FeatureFlags
{
    public class DefaultFeatureFlagsCache : IFeatureFlagsCache
    {
        public FeatureFlagsConfiguration? Configuration { get; set; }
    }
}
