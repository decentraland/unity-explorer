using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     Defines the functionalities for wearable catalog. Works like cache by storing instances of <see cref="IWearable" /> by string keys.
    /// </summary>
    public interface IWearableStorage : IAvatarElementStorage<IWearable, WearableDTO>
    {
    }
}
