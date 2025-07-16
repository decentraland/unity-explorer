using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.FeatureFlags
{
    /// <summary>
    /// Interface for feature providers that handle user-specific feature logic.
    /// Implement this interface for features that depend on user identity, allowlists, or other dynamic conditions.
    /// </summary>
    public interface IFeatureProvider
    {
        /// <summary>
        /// Checks if the feature is enabled for the current user.
        /// This method should handle all user-specific validation logic.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if the feature is enabled for the current user, false otherwise</returns>
        UniTask<bool> IsFeatureEnabledForUserAsync(CancellationToken ct);
    }
} 