using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.AssetLoad.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Linq;
using UnityEngine.Pool;

namespace DCL.SDKComponents.AssetLoad.Systems
{
    /// <summary>
    ///     Starts asset loading for assets listed in PBAssetLoad component
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_PRE_LOAD)]
    [ThrottlingEnabled]
    public partial class AssetLoadSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IPerformanceBudget frameTimeBudgetProvider;

        internal AssetLoadSystem(World world, ISceneData sceneData, IPerformanceBudget frameTimeBudgetProvider) : base(world)
        {
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
        }

        protected override void Update(float t)
        {
            StartAssetLoadingQuery(World);
            UpdateAssetLoadingQuery(World);
        }

        [Query]
        [None(typeof(AssetLoadComponent))]
        private void StartAssetLoading(in Entity entity, ref PBAssetLoad sdkComponent, ref PartitionComponent partitionComponent)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;

            AssetLoadComponent component = new AssetLoadComponent(sdkComponent.Assets);
            World.Add(entity, component);

            ProcessAssetList(ref sdkComponent, ref component, partitionComponent);
        }

        [Query]
        private void UpdateAssetLoading(ref PBAssetLoad sdkComponent, ref AssetLoadComponent component, ref PartitionComponent partitionComponent)
        {
            if (!sdkComponent.IsDirty) return;
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;
            ProcessAssetList(ref sdkComponent, ref component, partitionComponent);
        }

        private void ProcessAssetList(ref PBAssetLoad sdkComponent, ref AssetLoadComponent existingComponent, IPartitionComponent partitionComponent)
        {
            // Build set of current asset hashes
            var assetPathHash = DictionaryPool<string ,string>.Get();

            foreach (string assetPath in sdkComponent.Assets)
            {
                if (sceneData.TryGetHash(assetPath, out string hash))
                    assetPathHash.Add(assetPath, hash);
                else
                    ReportHub.LogWarning(GetReportData(), $"Asset {assetPath} not found in scene content");

                if (!existingComponent.LoadingAssetPaths.Contains(assetPath)
                    && existingComponent.LoadingEntities.TryGetValue(assetPath, out var loadingEntity)
                    && World.IsAlive(loadingEntity)
                    && World.TryGet(loadingEntity, out GetAssetBundleIntention intention))
                {
                    intention.CancellationTokenSource?.Cancel();
                    World.Destroy(loadingEntity);
                    existingComponent.LoadingEntities.Remove(assetPath);
                }
            }

            existingComponent.LoadingAssetPaths = sdkComponent.Assets;

            // Create loading entities for new assets
            foreach (var kvp in assetPathHash)
            {
                string path = kvp.Key;
                string hash = kvp.Value;

                // Skip if already loading
                if (existingComponent.LoadingEntities.ContainsKey(path))
                   continue;

                // Create asset bundle loading intention
                // ExpectedObjectType is null because we don't know what type of asset it is
                var intention = GetAssetBundleIntention.Create(
                    expectedAssetType: null,
                    hash: hash,
                    name: path
                );

                // Create a new entity for this asset loading
                Entity loadingEntity = World.Create(intention, new StreamableLoadingState(), partitionComponent);
                existingComponent.LoadingEntities.Add(path, loadingEntity);
            }

            DictionaryPool<string, string>.Release(assetPathHash);
        }
    }
}
