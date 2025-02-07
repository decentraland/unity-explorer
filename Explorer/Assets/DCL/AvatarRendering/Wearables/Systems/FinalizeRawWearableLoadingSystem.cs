using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.Textures;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
            IRealmData realmData,
            URLSubdirectory customStreamingSubdirectory
        ) : base(world, wearableStorage, realmData, customStreamingSubdirectory)
        {
        }

        protected override void Update(float t)
        {
            base.Update(t);

            bool defaultWearablesResolved = defaultWearablesState.GetDefaultWearablesState(World!).ResolvedState == DefaultWearablesComponent.State.Success;

            FinalizeRawGltfWearableLoadingQuery(World, defaultWearablesResolved);
            FinalizeRawFacialFeatureTexLoadingQuery(World, defaultWearablesResolved);
        }

        [Query]
        private void FinalizeRawGltfWearableLoading(
            [Data] bool defaultWearablesResolved,
            Entity entity,
            ref RawGltfPromise promise,
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            FinalizeAssetLoading<GLTFData, GetGLTFIntention>(defaultWearablesResolved, entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }

        [Query]
        private void FinalizeRawFacialFeatureTexLoading(
            [Data] bool defaultWearablesResolved,
            Entity entity,
            ref TexturePromise promise,
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            if (wearable.Type != WearableType.FacialFeature) return;

            FinalizeAssetLoading<Texture2DData, GetTextureIntention>(defaultWearablesResolved, entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }

        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<GLTFData> result, in BodyShape bodyShape, int index)
            => SetWearableResult(wearable, result.ToWearableAsset(wearable), bodyShape, index);

        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<Texture2DData> result, in BodyShape bodyShape, int index)
            => SetWearableResult(wearable, result.ToWearableAsset(wearable), bodyShape, index);

        protected override bool CreateAssetPromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            bool dtoHasContentDownloadUrl = !string.IsNullOrEmpty(component.DTO.ContentDownloadUrl);

            // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
            if (!dtoHasContentDownloadUrl) return false;

            if (component.TryCreateAssetPromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }
    }
}
