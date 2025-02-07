using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Runtime.CompilerServices;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeAssetBundleWearableLoadingSystemBase : FinalizeWearableLoadingSystemBase
    {
        public FinalizeAssetBundleWearableLoadingSystemBase(
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

            FinalizeAssetBundleLoadingQuery(World, defaultWearablesResolved);
        }

        [Query]
        private void FinalizeAssetBundleLoading(
            [Data] bool defaultWearablesResolved,
            Entity entity,
            ref AssetBundlePromise promise,
            ref IWearable wearable,
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

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<AssetBundleData> result))
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

        protected override bool CreateAssetPromiseIfRequired(IWearable component, in GetWearablesByPointersIntention intention, IPartitionComponent partitionComponent)
        {
            bool dtoHasContentDownloadUrl = !string.IsNullOrEmpty(component.DTO.ContentDownloadUrl);

            // Do not repeat the promise if already failed once. Otherwise it will end up in an endless loading:true state
            if (!dtoHasContentDownloadUrl && component.ManifestResult is { Succeeded: false }) return false;

            if (EnumUtils.HasFlag(intention.PermittedSources, AssetSource.WEB) // Manifest is required for Web loading only
                && !dtoHasContentDownloadUrl && component.ManifestResult == null)
                return component.CreateAssetBundleManifestPromise(World, intention.BodyShape, intention.CancellationTokenSource, partitionComponent);

            if (component.TryCreateAssetPromise(in intention, customStreamingSubdirectory, partitionComponent, World, GetReportCategory()))
            {
                component.UpdateLoadingStatus(true);
                return true;
            }

            return false;
        }

        // Asset Bundle Wearable
        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<AssetBundleData> result, in BodyShape bodyShape, int index)
            => SetWearableResult(wearable, result.ToWearableAsset(wearable), bodyShape, index);
    }
}
