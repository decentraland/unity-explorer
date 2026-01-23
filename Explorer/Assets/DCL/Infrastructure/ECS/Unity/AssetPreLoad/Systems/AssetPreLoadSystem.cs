using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using CRDT;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.AudioSources;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.AssetLoad.Components;
using SceneRunner.Scene;
using System;
using AudioPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;
using TexturePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.Unity.AssetLoad.Systems
{
    /// <summary>
    ///     Starts asset loading for assets listed in PBAssetLoad component
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.ASSET_PRE_LOAD)]
    [ThrottlingEnabled]
    public partial class AssetPreLoadSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IPerformanceBudget frameTimeBudgetProvider;
        private readonly AssetPreLoadUtils assetPreLoadUtils;

        internal AssetPreLoadSystem(World world,
            ISceneData sceneData,
            IPerformanceBudget frameTimeBudgetProvider,
            AssetPreLoadUtils assetPreLoadUtils)
            : base(world)
        {
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.assetPreLoadUtils = assetPreLoadUtils;
        }

        protected override void Update(float t)
        {
            StartAssetLoadingQuery(World);
            UpdateAssetLoadingQuery(World);
        }

        [Query]
        [None(typeof(AssetPreLoadComponent))]
        private void StartAssetLoading(in Entity entity, ref PBAssetLoad sdkComponent, ref CRDTEntity crdtEntity)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;

            AssetPreLoadComponent component = AssetPreLoadComponent.Create();
            World.Add(entity, component);

            ProcessAssetList(crdtEntity, ref sdkComponent, ref component);
        }

        [Query]
        private void UpdateAssetLoading(in CRDTEntity entity, ref PBAssetLoad sdkComponent, ref AssetPreLoadComponent component)
        {
            if (!sdkComponent.IsDirty) return;
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;
            ProcessAssetList(entity, ref sdkComponent, ref component);
        }

        private void ProcessAssetList(in CRDTEntity crdtEntity, ref PBAssetLoad sdkComponent, ref AssetPreLoadComponent existingComponent)
        {
            foreach (string path in sdkComponent.Assets)
            {
                if (existingComponent.LoadingAssetPaths.Contains(path)) continue;

                if (!sceneData.TryGetHash(path, out string hash))
                {
                    SendUpdateAndStore(path, crdtEntity, LoadingState.NotFound, ref existingComponent);
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} not found in scene content");
                    continue;
                }

                AssetPreLoadChildComponent assetPreLoadChildComponent = new AssetPreLoadChildComponent(crdtEntity, hash, path);

                // Supported formats https://docs.decentraland.org/creator/scene-editor/build/import-items#supported-formats
                if (path.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".ogg", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!AudioUtils.TryCreateAudioClipPromise(World, sceneData, path, PartitionComponent.MIN_PRIORITY, out AudioPromise? assetPromise))
                    {
                        SendUpdateAndStore(path, crdtEntity, LoadingState.FinishedWithError, ref existingComponent);
                        continue;
                    }

                    World.Create(assetPromise!.Value, PartitionComponent.MIN_PRIORITY, assetPreLoadChildComponent);
                }
                else if (path.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBVideoPlayer component = new PBVideoPlayer
                    {
                        Src = path,
                    };
                    World.Create(component, PartitionComponent.MIN_PRIORITY, assetPreLoadChildComponent);
                }
                else if (path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpeg", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!sceneData.TryGetContentUrl(path, out URLAddress contentUrl))
                    {
                        SendUpdateAndStore(path, crdtEntity, LoadingState.NotFound, ref existingComponent);
                        continue;
                    }

                    var promise = TexturePromise.Create(World,
                        new GetTextureIntention
                        {
                            CommonArguments = new CommonLoadingArguments(contentUrl),
                            ReportSource = GetReportCategory(),
                        },
                        PartitionComponent.MIN_PRIORITY);
                    World.Create(promise, PartitionComponent.MIN_PRIORITY, assetPreLoadChildComponent);
                }
                else if (path.EndsWith(".glTF", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".glb", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBGltfContainer component = new PBGltfContainer
                    {
                        Src = path,
                    };
                    World.Create(component, PartitionComponent.MIN_PRIORITY, assetPreLoadChildComponent);
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} has unsupported format");
                    continue;
                }

                SendUpdateAndStore(path, crdtEntity, LoadingState.Loading, ref existingComponent);
            }
        }

        private void SendUpdateAndStore(string path, CRDTEntity crdtEntity, LoadingState loadingState, ref AssetPreLoadComponent existingComponent)
        {
            assetPreLoadUtils.AppendAssetLoadingMessage(crdtEntity, loadingState, path);
            existingComponent.LoadingAssetPaths.Add(path);
        }
    }
}
