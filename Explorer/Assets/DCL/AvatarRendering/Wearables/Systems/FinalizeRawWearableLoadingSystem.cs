using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeRawWearableLoadingSystem : FinalizeWearableLoadingSystemBase
    {
        public FinalizeRawWearableLoadingSystem(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData
        ) : base(world, wearableStorage, realmData)
        {
        }

        protected override void Update(float t)
        {
            base.Update(t);

            FinalizeRawGltfWearableLoadingQuery(World);
            FinalizeRawFacialFeatureTexLoadingQuery(World);
        }

        [Query]
        private void FinalizeRawGltfWearableLoading(
            Entity entity,
            ref RawGltfPromise promise,
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            FinalizeAssetLoading(entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset());
        }

        [Query]
        private void FinalizeRawFacialFeatureTexLoading(
            Entity entity,
            ref TexturePromise promise,
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            if (wearable.Type != WearableType.FacialFeature) return;

            FinalizeAssetLoading(entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset());
        }
    }
}
