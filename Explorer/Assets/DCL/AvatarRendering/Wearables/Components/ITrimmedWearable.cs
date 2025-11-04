using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Helpers;

namespace DCL.AvatarRendering.Wearables.Components
{
    public interface ITrimmedWearable : ITrimmedAvatarAttachment<TrimmedWearableDTO>
    {

        /// <summary>
        ///     Per <see cref="BodyShape" /> [MALE, FEMALE]
        /// </summary>
        // WearableAssets[] WearableAssetResults { get; }

        bool IsCompatibleWithBodyShape(string bodyShape);

        public static ITrimmedWearable NewEmpty() =>
            new TrimmedWearable();
    }
}
