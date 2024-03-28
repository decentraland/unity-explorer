using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarInstantiationPolymorphicBehaviour
    {
        [CanBeNull]
        public static GameObject AppendToAvatar(
            this IWearable resultWearable,
            IWearableAssetsCache wearableAssetsCache,
            ISet<string> usedCategories,
            ref FacialFeaturesTextures facialFeaturesTextures,
            ref AvatarShapeComponent avatarShapeComponent,
            Transform parent,
            string reportCategory)
        {
            var originalAssets = resultWearable.WearableAssetResults[avatarShapeComponent.BodyShape].Results;
            var mainAsset = originalAssets[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]!.Value.Asset!;

            var category = resultWearable.GetCategory();

            switch (resultWearable.Type)
            {
                case WearableType.FacialFeature:

                    var texturesSet = facialFeaturesTextures.Value[category];
                    texturesSet[TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE] = ((WearableTextureAsset)mainAsset).Texture;

                    // Mask is optional
                    var maskAssetRes = originalAssets[WearablePolymorphicBehaviour.MASK_ASSET_INDEX];
                    if (maskAssetRes is { Asset: not null })
                        texturesSet[TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID] = ((WearableTextureAsset)maskAssetRes.Value.Asset!).Texture;

                    return null;
                default:
                {
                    var regularAsset = (WearableRegularAsset)mainAsset;

                    CachedWearable instantiatedWearable =
                        wearableAssetsCache.InstantiateWearable(regularAsset, parent);

                    avatarShapeComponent.InstantiatedWearables.Add(instantiatedWearable);

                    usedCategories.Add(category);

                    return instantiatedWearable;
                }
            }
        }

        public static void Dereference(this in AvatarShapeComponent avatarShapeComponent)
        {
            var resolution = avatarShapeComponent.WearablePromise.Result;

            if (!resolution.HasValue) return;
            var wearables = resolution.Value.Asset.Wearables;

            foreach (IWearable wearable in wearables)
            {
                ref var assets = ref wearable.WearableAssetResults[avatarShapeComponent.BodyShape];

                for (var i = 0; i < assets.Results.Length; i++)
                    assets.Results[i]?.Asset?.Dereference();
            }
        }
    }
}
