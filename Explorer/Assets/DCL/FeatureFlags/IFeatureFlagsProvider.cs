using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.FeatureFlags
{
    public interface IFeatureFlagsProvider
    {
        UniTask<FeatureFlagsConfiguration> GetAsync(FeatureFlagOptions options, CancellationToken ct);
    }
}
