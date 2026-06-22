using DCL.AvatarRendering.Loading;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Emotes
{
    /// <summary>
    ///     Unloads the wearable from the catalog by a frame time budget provider.
    /// </summary>
    /// <param name="frameTimeBudget">The frame time budget provider.</param>
    public interface ITrimmedEmoteStorage : ITrimmedAvatarElementStorage<ITrimmedEmote, TrimmedEmoteDTO>
    {
    }
}
