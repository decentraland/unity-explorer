#if UNITY_EDITOR || DEVELOPMENT_BUILD

using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableCatalog
    {
        public int WearableAssetsInCatalog
        {
            get
            {
                var sum = 0;

                foreach (IWearable wearable in wearableDictionary.Values)
                    if (wearable.WearableAssetResults != null)
                        foreach (StreamableLoadingResult<WearableAsset>? result in wearable.WearableAssetResults)
                            if (result is { Asset: not null })
                                sum++;

                return sum;
            }
        }
    }
}

#endif
