using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.AssetLoad.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.SDKComponents.AssetLoad.Systems
{
    /// <summary>
    ///     Tracks loading state of all assets and writes it back to PBAssetLoadLoadingState
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_PRE_LOAD)]
    public partial class WriteAssetLoadLoadingStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal WriteAssetLoadLoadingStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            UpdateLoadingStateQuery(World);
        }

        [Query]
        private void UpdateLoadingState(in Entity entity, ref PBAssetLoad sdkComponent, ref AssetLoadComponent component, CRDTEntity sdkEntity)
        {
            if (component.LoadingEntities.Count == 0)
            {
                // No assets to load
                ecsToCRDTWriter.PutMessage<PBAssetLoadLoadingState, LoadingState>(
                    static (pbComponent, loadingState) => pbComponent.CurrentState = loadingState,
                    sdkEntity,
                    LoadingState.Finished
                );
                return;
            }

            int loadingCount = 0;
            int finishedCount = 0;
            int errorCount = 0;
            int notFoundCount = 0;

            // Check state of all loading entities
            var hashesToRemove = new List<string>();
            foreach (var kvp in component.LoadingEntities)
            {
                string hash = kvp.Key;
                Entity loadingEntity = kvp.Value;

                if (!World.IsAlive(loadingEntity))
                {
                    hashesToRemove.Add(hash);
                    continue;
                }

                // Check if loading is complete
                if (World.TryGet(loadingEntity, out StreamableLoadingResult<AssetBundleData> result))
                {
                    if (result.Succeeded)
                        finishedCount++;
                    else
                        errorCount++;
                }
                else if (World.Has<GetAssetBundleIntention>(loadingEntity))
                {
                    // Still loading
                    loadingCount++;
                }
                else
                {
                    // Entity exists but no intention or result - might be in transition
                    loadingCount++;
                }
            }

            // Clean up dead entities
            foreach (string hash in hashesToRemove)
            {
                component.LoadingEntities.Remove(hash);
            }

            if (hashesToRemove.Count > 0)
            {
                World.Set(entity, component);
            }

            // Determine overall state
            LoadingState overallState;
            if (loadingCount > 0)
            {
                overallState = LoadingState.Loading;
            }
            else if (errorCount > 0 || notFoundCount > 0)
            {
                overallState = LoadingState.FinishedWithError;
            }
            else if (finishedCount == component.LoadingEntities.Count)
            {
                overallState = LoadingState.Finished;
            }
            else
            {
                overallState = LoadingState.Unknown;
            }

            // Write state back to CRDT
            ecsToCRDTWriter.PutMessage<PBAssetLoadLoadingState, LoadingState>(
                static (pbComponent, loadingState) => pbComponent.CurrentState = loadingState,
                sdkEntity,
                overallState
            );
        }
    }
}
