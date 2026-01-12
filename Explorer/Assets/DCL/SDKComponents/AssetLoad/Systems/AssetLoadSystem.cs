using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.AssetLoad.Components;
using DCL.SDKComponents.AudioSources;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using SceneRunner.Scene;
using System;
using UnityEngine.Pool;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
        private readonly AssetLoadUtils assetLoadUtils;

        internal AssetLoadSystem(World world,
            ISceneData sceneData,
            IPerformanceBudget frameTimeBudgetProvider,
            AssetLoadUtils assetLoadUtils)
            : base(world)
        {
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.assetLoadUtils = assetLoadUtils;
        }

        protected override void Update(float t)
        {
            StartAssetLoadingQuery(World);
            UpdateAssetLoadingQuery(World);
        }

        [Query]
        [None(typeof(AssetLoadComponent))]
        private void StartAssetLoading(in Entity entity, ref PBAssetLoad sdkComponent, ref CRDTEntity crdtEntity)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;

            AssetLoadComponent component = new AssetLoadComponent(sdkComponent.Assets);
            World.Add(entity, component);

            ProcessAssetList(crdtEntity, ref sdkComponent, ref component);
        }

        [Query]
        private void UpdateAssetLoading(in CRDTEntity entity, ref PBAssetLoad sdkComponent, ref AssetLoadComponent component)
        {
            if (!sdkComponent.IsDirty) return;
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;
            ProcessAssetList(entity, ref sdkComponent, ref component);
        }

        private void ProcessAssetList(in CRDTEntity crdtEntity, ref PBAssetLoad sdkComponent, ref AssetLoadComponent existingComponent)
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

                AssetLoadUtils.RemoveAssetLoading(World, loadingEntity, assetPath, ref existingComponent);
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
                    AudioUtils.TryCreateAudioClipPromise(World, sceneData, path, PartitionComponent.MIN_PRIORITY, out AudioPromise? assetPromise);

                    loadingEntity = World.Create(assetPromise, PartitionComponent.MIN_PRIORITY, new AssetLoadChildComponent(crdtEntity));
                }
                else if (path.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBVideoPlayer component = new PBVideoPlayer
                    {
                        Src = path,
                    };
                    loadingEntity = World.Create(component, PartitionComponent.MIN_PRIORITY, new AssetLoadChildComponent(crdtEntity));
                }
                else if (path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpeg", StringComparison.InvariantCultureIgnoreCase))
                {
                    var promise = TexturePromise.Create(World,
                        new GetTextureIntention
                        {
                            CommonArguments = new CommonLoadingArguments(URLAddress.FromString(path)),
                            ReportSource = GetReportCategory(),
                        },
                        PartitionComponent.MIN_PRIORITY);
                    loadingEntity = World.Create(promise, PartitionComponent.MIN_PRIORITY, new AssetLoadChildComponent(crdtEntity));
                }
                else if (path.EndsWith(".glTF", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".glb", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBGltfContainer component = new PBGltfContainer
                    {
                        Src = path,
                    };
                    loadingEntity = World.Create(component, PartitionComponent.MIN_PRIORITY, new AssetLoadChildComponent(crdtEntity));
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} has unsupported format");
                    continue;
                }

                assetLoadUtils.AppendAssetLoadingMessage(crdtEntity, LoadingState.Loading, path);

                existingComponent.LoadingEntities.Add(path, loadingEntity);
            }
            existingComponent.LoadingAssetPaths = sdkComponent.Assets;

            DictionaryPool<string, string>.Release(assetPathHash);
        }
    }
}
