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
using System.Runtime.CompilerServices;
using RawGltfPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.GLTF.GLTFData, ECS.StreamableLoading.GLTF.GetGLTFIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeRawWearableLoadingSystemBase : FinalizeWearableLoadingSystemBase
    {
        public FinalizeRawWearableLoadingSystemBase(
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
            in IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetWearableResultOnCancellation(wearable, in bodyShape, index);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<GLTFData> result))
            {
                // every asset in the batch is mandatory => if at least one has already failed set the default wearables
                if (result.Succeeded && !AnyAssetHasFailed(wearable, bodyShape))
                    SetWearableResult(wearable, result, in bodyShape, index);
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.UpdateLoadingStatus(!AllAssetsAreLoaded(wearable, bodyShape));
                World.Destroy(entity);
            }
        }

        [Query]
        private void FinalizeRawFacialFeatureTexLoading(
            [Data] bool defaultWearablesResolved,
            Entity entity,
            ref TexturePromise promise,
            in IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            if (wearable.Type != WearableType.FacialFeature) return;

            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetWearableResultOnCancellation(wearable, in bodyShape, index);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.TryConsume(World, out StreamableLoadingResult<Texture2DData> result))
            {
                // every asset in the batch is mandatory => if at least one has already failed set the default wearables
                if (result.Succeeded && !AnyAssetHasFailed(wearable, bodyShape))
                    SetWearableResult(wearable, result, in bodyShape, index);
                else
                    SetDefaultWearables(defaultWearablesResolved, wearable, in bodyShape);

                wearable.UpdateLoadingStatus(!AllAssetsAreLoaded(wearable, bodyShape));
                World.Destroy(entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllAssetsAreLoaded(IWearable wearable, BodyShape bodyShape)
        {
            for (var i = 0; i < wearable.WearableAssetResults[bodyShape].Results.Length; i++)
                if (wearable.WearableAssetResults[bodyShape].Results[i] is not { IsInitialized: true })
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AnyAssetHasFailed(IWearable wearable, BodyShape bodyShape) =>
            wearable.WearableAssetResults[bodyShape].ReplacedWithDefaults;

        // Raw GLTF Wearable
        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<GLTFData> result, in BodyShape bodyShape, int index)
            => SetWearableResult(wearable, result.ToWearableAsset(wearable), bodyShape, index);

        // Raw Facial Feature Wearable
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
