using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Unloads the wearable from the catalog by a frame time budget provider.
    /// </summary>
    /// <param name="frameTimeBudget">The frame time budget provider.</param>
    public interface ITrimmedWearableStorage : ITrimmedAvatarElementStorage<ITrimmedWearable, TrimmedWearableDTO>
    {
    }
}
