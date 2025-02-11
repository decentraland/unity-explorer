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
using ECS.StreamableLoading.Common.Components;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeAssetBundleWearableLoadingSystem : FinalizeWearableLoadingSystemBase
    {
        public FinalizeAssetBundleWearableLoadingSystem(
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
            IWearable wearable,
            in BodyShape bodyShape,
            int index
        )
        {
            FinalizeAssetLoading<AssetBundleData, GetAssetBundleIntention>(defaultWearablesResolved, entity, ref promise, wearable, in bodyShape, index, result => result.ToWearableAsset(wearable));
        }

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
    }
}
