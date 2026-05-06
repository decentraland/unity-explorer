using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarInstantiationPolymorphicBehaviour
    {
        public static GameObject? AppendToAvatar(
            this IWearable resultWearable,
            IAttachmentsAssetsCache wearableAssetsCache,
            ISet<string> usedCategories,
            ref FacialFeaturesTextures facialFeaturesTextures,
            ref AvatarShapeComponent avatarShapeComponent,
            Transform parent)
        {
            var originalAssets = resultWearable.WearableAssetResults[avatarShapeComponent.BodyShape].Results;

            if (originalAssets?[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX]?.Asset == null)
            {
                ReportHub.LogError(ReportCategory.WEARABLE, $"Cannot find the asset for wearable with ID {resultWearable.DTO.id} name {resultWearable.DTO.Metadata.name} and body shape {avatarShapeComponent.BodyShape.Value}");
                return null;
            }

            var mainAsset = originalAssets[WearablePolymorphicBehaviour.MAIN_ASSET_INDEX].Value.Asset!;

            string category = resultWearable.GetCategory();

            switch (resultWearable.Type)
            {
                case WearableType.FacialFeature:

                    var texturesSet = facialFeaturesTextures.Value[category];
                    texturesSet[TextureArrayConstants.MAINTEX_ORIGINAL_TEXTURE] = ((AttachmentTextureAsset)mainAsset).Texture;

                    // Mask is optional
                    var maskAssetRes = originalAssets[WearablePolymorphicBehaviour.MASK_ASSET_INDEX];

                    if (maskAssetRes is { Asset: not null })
                        texturesSet[TextureArrayConstants.MASK_ORIGINAL_TEXTURE_ID] = ((AttachmentTextureAsset)maskAssetRes.Value.Asset!).Texture;

                    return null;
                default:
                {
                    var regularAsset = (AttachmentRegularAsset)mainAsset;

                    IReadOnlyDictionary<string, SpringBoneParamsDto>? boneParams = ResolveSpringBoneParams(resultWearable, avatarShapeComponent.BodyShape);

                    var instantiatedWearable =
                        wearableAssetsCache.InstantiateWearable(regularAsset, parent, resultWearable.IsOutlineCompatible(), boneParams);

                    avatarShapeComponent.InstantiatedWearables.Add(instantiatedWearable);

                    usedCategories.Add(category);

                    return instantiatedWearable;
                }
            }
        }

        private static IReadOnlyDictionary<string, SpringBoneParamsDto>? ResolveSpringBoneParams(IWearable wearable, BodyShape bodyShape)
        {
            WearableDTO? wearableDto = wearable.Model.Asset;
            if (wearableDto == null) return null;

            SpringBonesDto? springBones = wearableDto.metadata?.data?.springBones;
            if (springBones?.models == null) return null;

            if (springBones.version != SpringBonesDto.SUPPORTED_VERSION)
                ReportHub.LogWarning(ReportCategory.AVATAR,
                    $"Spring bones DTO version {springBones.version} does not match supported version {SpringBonesDto.SUPPORTED_VERSION}; parsing may misinterpret fields. Wearable: {wearable.GetUrn()}");

            if (!wearable.TryGetMainFileHash(bodyShape, out string? mainFileHash) || mainFileHash == null)
                return null;

            return springBones.models.TryGetValue(mainFileHash, out var map) ? map : null;
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
