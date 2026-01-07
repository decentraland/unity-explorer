using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.AssetLoad.Components;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using UnityEngine.Pool;
using UnityEngine;

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
                if (sceneData.TryGetHash(assetPath, out string hash))
                    assetPathHash.Add(assetPath, hash);
                else
                    ReportHub.LogWarning(GetReportData(), $"Asset {assetPath} not found in scene content");

            var toRemove = ListPool<string>.Get();
            foreach (var kvp in existingComponent.LoadingEntities)
            {
                string assetPath = kvp.Key;
                if (!assetPathHash.ContainsKey(assetPath))
                    toRemove.Add(assetPath);
            }

            foreach (string assetPath in toRemove)
            {
                if (!existingComponent.LoadingEntities.TryGetValue(assetPath, out var loadingEntity)) continue;

                //TODO: stop each loading properly and then destroy
                World.Destroy(loadingEntity);
                existingComponent.LoadingEntities.Remove(assetPath);
            }
            ListPool<string>.Release(toRemove);

            // Create loading entities for new assets
            foreach (var kvp in assetPathHash)
            {
                string path = kvp.Key;
                string hash = kvp.Value;

                // Skip if already loading
                if (existingComponent.LoadingEntities.ContainsKey(path))
                   continue;

                Entity loadingEntity;

                // Supported formats https://docs.decentraland.org/creator/scene-editor/build/import-items#supported-formats
                if (path.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".ogg", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBAudioSource component = new PBAudioSource
                    {
                        AudioClipUrl = path,
                    };
                    loadingEntity = World.Create(component, PartitionComponent.MIN_PRIORITY);
                }
                else if (path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpeg", StringComparison.InvariantCultureIgnoreCase))
                {
                    var intention = new GetTextureIntention(
                        url: path,
                        fileHash: hash,
                        wrapMode: TextureWrapMode.Clamp,
                        filterMode: FilterMode.Bilinear,
                        textureType: TextureType.Albedo,
                        reportSource: nameof(AssetLoadSystem),
                        attemptsCount: StreamableLoadingDefaults.ATTEMPTS_COUNT
                    );
                    loadingEntity = World.Create(intention, PartitionComponent.MIN_PRIORITY);
                }
                else if (path.EndsWith(".glTF", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".glb", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBGltfContainer component = new PBGltfContainer
                    {
                        Src = path,
                    };
                    loadingEntity = World.Create(component, PartitionComponent.MIN_PRIORITY);
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} has unsupported format");
                    continue;
                }

                existingComponent.LoadingEntities.Add(path, loadingEntity);
            }
            existingComponent.LoadingAssetPaths = sdkComponent.Assets;

            DictionaryPool<string, string>.Release(assetPathHash);
        }
    }
}
