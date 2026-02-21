using DCL.AvatarRendering.Loading;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.AvatarRendering.Emotes
{
    public interface ITrimmedEmoteStorage : ITrimmedAvatarElementStorage<ITrimmedEmote, TrimmedEmoteDTO>
    {
        /// <summary>
        ///     Unloads the wearable from the catalog by a frame time budget provider.
        /// </summary>
        /// <param name="frameTimeBudget">The frame time budget provider.</param>
        void Unload(IPerformanceBudget frameTimeBudget);
    }
}
