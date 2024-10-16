﻿#if UNITY_EDITOR || DEVELOPMENT_BUILD

using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Components;
using ECS.StreamableLoading.Common.Components;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    public partial class WearableStorage
    {
        public int WearableAssetsInCatalog
        {
            get
            {
                var sum = 0;

                foreach (IWearable wearable in wearablesCache.Values)
                    if (wearable.WearableAssetResults != null)
                        foreach (WearableAssets assets in wearable.WearableAssetResults)
                            if (assets.Results != null)
                                foreach (StreamableLoadingResult<AttachmentAssetBase>? result in assets.Results)
                                    if (result is { Asset: not null })
                                        sum++;

                return sum;
            }
        }
    }
}

#endif
