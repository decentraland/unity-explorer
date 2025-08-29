using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.FeatureFlags
{
    /// <summary>
    /// Interface for feature providers that handle more complex validation logic.
    /// Implement this interface for features that depend on user identity, allowlists, or other dynamic conditions.
    /// </summary>
    public interface IFeatureProvider
    {
        /// <summary>
        /// Checks if the feature is enabled using the specific provider logic.
        /// </summary>
        UniTask<bool> IsFeatureEnabledAsync(CancellationToken ct);
    }
}
