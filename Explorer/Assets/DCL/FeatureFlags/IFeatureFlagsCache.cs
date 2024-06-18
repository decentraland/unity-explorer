namespace DCL.FeatureFlags
{
    public interface IFeatureFlagsCache
    {
        FeatureFlagsConfiguration? Configuration { get; set; }
    }
}
