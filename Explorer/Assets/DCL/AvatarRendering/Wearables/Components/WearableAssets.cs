using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Components
{
    /// <summary>
    ///     One asset - regular wearable,
    ///     Two assets - Facial feature,
    /// </summary>
    public struct WearableAssets
    {
        /// <summary>
        ///     If the element is null it's a signal that the intent to load it was not yet created
        /// </summary>
        public StreamableLoadingResult<WearableAssetBase>?[] Results;

        /// <summary>
        ///     Whether the results were replaced with default ones
        /// </summary>
        public bool ReplacedWithDefaults;

        public void AddReference()
        {
            for (var i = 0; i < Results.Length; i++)
                Results[i]?.Asset?.AddReference();
        }
    }
}
