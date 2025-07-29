using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using ECS;
using ECS.Abstract;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class FinalizeWearableLoadingSystemBase : FinalizeElementsLoadingSystem<GetWearableDTOByPointersIntention, IWearable, WearableDTO, WearablesDTOList>
    {
        private readonly IRealmData realmData;
        private readonly IWearableStorage wearableStorage;
        private SingleInstanceEntity defaultWearablesState;

        public FinalizeWearableLoadingSystemBase(
            World world,
            IWearableStorage wearableStorage,
            IRealmData realmData
        ) : base(world, wearableStorage, WearableComponentsUtils.POINTERS_POOL)
        {
            this.wearableStorage = wearableStorage;
            this.realmData = realmData;
        }

        public override void Initialize()
        {
        }

        protected override void Update(float t)
        {
            // Only DTO loading requires realmData
            if (realmData.Configured)
                FinalizeWearableDTOQuery(World);
        }

        [Query]
        private void FinalizeWearableDTO(Entity entity, ref AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> promise, ref BodyShape bodyShape)
        {
            if (TryFinalizeIfCancelled(entity, promise))
                return;

            if (promise.SafeTryConsume(World!, GetReportCategory(), out StreamableLoadingResult<WearablesDTOList> promiseResult))
            {
                if (!promiseResult.Succeeded)
                {
                    //No wearable representation is going to be possible
                    foreach (string pointerID in promise.LoadingIntention.Pointers)
                        ReportAndFinalizeWithError(pointerID);
                }
                else
                {
                    using var _ = WearableComponentsUtils.POINTERS_POOL.Get(out var failedDTOList);
                    failedDTOList!.AddRange(promise.LoadingIntention.Pointers);

                    using (var list = promise.Result.Value.Asset.ConsumeAttachments())
                        foreach (WearableDTO assetEntity in list.Value)
                        {
                            if (wearableStorage.TryGetElementWithLogs(assetEntity, GetReportCategory(), out var component) == false)
                                continue;

                            if (component!.TryResolveDTO(new StreamableLoadingResult<WearableDTO>(assetEntity)) == false)
                                ReportHub.LogError(GetReportData(), $"Wearable DTO has already been initialized: {assetEntity.Metadata.id}");

                            failedDTOList.Remove(assetEntity.Metadata.id);
                            component.UpdateLoadingStatus(false);
                        }

                    //If this list is not empty, it means we have at least one unresolvedDTO that was not completed. We need to finalize it as error
                    foreach (var urn in failedDTOList)
                        ReportAndFinalizeWithError(urn);
                }

                promise.LoadingIntention.ReleasePointers();
                World.Destroy(entity);
            }
        }

        protected void SetAsFailed(IWearable wearable, in BodyShape bodyShape)
        {
            StreamableLoadingResult<AttachmentAssetBase> failedResult = new StreamableLoadingResult<AssetBundleData>(
                GetReportData(),
                new Exception($"Default wearable {wearable.DTO.GetHash()} failed to load")
            ).ToWearableAsset(wearable);

            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                SetFailure(BodyShape.MALE);
                SetFailure(BodyShape.FEMALE);
            }
            else
                SetFailure(bodyShape);

            return;

            void SetFailure(BodyShape bs)
            {
                // the destination array might be not created if DTO itself has failed to load
                ref var result = ref wearable.WearableAssetResults[bs];
                result.Results ??= new StreamableLoadingResult<AttachmentAssetBase>?[1]; // default capacity, can't tell without the DTO
                result.Results[0] = failedResult;
            }
        }

        /// <summary>
        ///     If the loading of the asset was cancelled reset the promise so we can start downloading it again with a new intent
        /// </summary>
        private static void ResetWearableResultOnCancellation(IWearable wearable, in BodyShape bodyShape, int index)
        {
            wearable.UpdateLoadingStatus(false);

            void ResetBodyShape(BodyShape bs)
            {
                ref WearableAssets assets = ref wearable.WearableAssetResults[bs];
                if (assets.Results == null) return;

                if (assets.Results[index] is { IsInitialized: false })
                    assets.Results[index] = null;
            }

            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                ResetBodyShape(BodyShape.MALE);
                ResetBodyShape(BodyShape.FEMALE);
            }
            else
                ResetBodyShape(bodyShape);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AllAssetsAreLoaded(IWearable wearable, BodyShape bodyShape)
        {
            if (wearable.WearableAssetResults[bodyShape].Results == null) return false;

            for (var i = 0; i < wearable.WearableAssetResults[bodyShape].Results!.Length; i++)
                if (wearable.WearableAssetResults[bodyShape].Results![i] is { IsInitialized: false })
                    return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AnyAssetHasFailed(IWearable wearable, BodyShape bodyShape)
        {
            return wearable.WearableAssetResults[bodyShape].Results == null
                   || wearable.WearableAssetResults[bodyShape].Results!.Any(result => result is { Succeeded: false });
        }

        private static void SetWearableResult(IWearable wearable, StreamableLoadingResult<AttachmentAssetBase> wearableResult, in BodyShape bodyShape, int index)
        {
            if (wearable.IsUnisex() && wearable.HasSameModelsForAllGenders())
            {
                SetByRef(BodyShape.MALE);
                SetByRef(BodyShape.FEMALE);
            }
            else
                SetByRef(bodyShape);

            return;

            void SetByRef(BodyShape bodyShape)
            {
                ref var asset = ref wearable.WearableAssetResults[bodyShape];
                asset.Results[index] = wearableResult;
            }
        }

        protected void FinalizeAssetLoading<TAsset, TLoadingIntention>(
            Entity entity,
            ref AssetPromise<TAsset, TLoadingIntention> promise,
            in IWearable wearable,
            in BodyShape bodyShape,
            int index,
            Func<StreamableLoadingResult<TAsset>, StreamableLoadingResult<AttachmentAssetBase>> toWearableAsset
        ) where TLoadingIntention : IAssetIntention, IEquatable<TLoadingIntention>
        {
            if (promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested)
            {
                ResetWearableResultOnCancellation(wearable, in bodyShape, index);
                promise.ForgetLoading(World);
                World.Destroy(entity);
                return;
            }

            if (promise.SafeTryConsume(World, GetReportCategory(), out StreamableLoadingResult<TAsset> result))
            {
                // every asset in the batch is mandatory => if at least one has already failed set the default wearables
                if (result.Succeeded && !AnyAssetHasFailed(wearable, bodyShape))
                    SetWearableResult(wearable, toWearableAsset(result), in bodyShape, index);
                else
                    SetAsFailed(wearable, in bodyShape);

                wearable.UpdateLoadingStatus(!AllAssetsAreLoaded(wearable, bodyShape));
                World.Destroy(entity);
            }
        }
    }
}
