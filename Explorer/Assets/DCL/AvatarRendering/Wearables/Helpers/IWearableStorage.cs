using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Defines the functionalities for wearable catalog. Works like cache by storing instances of <see cref="IWearable" /> by string keys.
    /// </summary>
    public interface IWearableStorage : IAvatarElementStorage<IWearable, WearableDTO>
    {
        /// <summary>
        ///     Retrieves default wearable from the catalog.
        /// </summary>
        /// <param name="bodyShape">The body shape.</param>
        /// <param name="category">The category.</param>
        /// <returns>An instance of the <see cref="IWearable" /> type.</returns>
        IWearable? GetDefaultWearable(BodyShape bodyShape, string category);
    }
}
