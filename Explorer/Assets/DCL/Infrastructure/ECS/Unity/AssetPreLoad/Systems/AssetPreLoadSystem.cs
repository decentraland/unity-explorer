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

        internal AssetPreLoadSystem(World world,
            ISceneData sceneData,
            IPerformanceBudget frameTimeBudgetProvider)
            : base(world)
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
        [None(typeof(AssetPreLoadComponent))]
        private void StartAssetLoading(in Entity entity, ref PBAssetLoad sdkComponent, in CRDTEntity crdtEntity)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;

            AssetPreLoadComponent component = AssetPreLoadComponent.Empty;
            World.Add(entity, component);

            ProcessAssetList(crdtEntity, in sdkComponent, ref component);
        }

        [Query]
        private void UpdateAssetLoading(in CRDTEntity entity, ref PBAssetLoad sdkComponent, ref AssetPreLoadComponent component)
        {
            if (!sdkComponent.IsDirty) return;
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;

            sdkComponent.IsDirty = false;
            ProcessAssetList(entity, in sdkComponent, ref component);
        }

        private void ProcessAssetList(in CRDTEntity crdtEntity, in PBAssetLoad sdkComponent, ref AssetPreLoadComponent existingComponent)
        {
            foreach (string path in sdkComponent.Assets)
            {
                if (existingComponent.LoadingAssetPaths.Contains(path)) continue;

                AssetPreLoadLoadingStateComponent loadingStateComponent = new AssetPreLoadLoadingStateComponent(crdtEntity, path);
                Entity createdEntity = World.Create(PartitionComponent.MIN_PRIORITY);

                if (!sceneData.TryGetHash(path, out string hash))
                {
                    MarkForUpdateAndStore(LoadingState.NotFound, createdEntity, ref existingComponent, ref loadingStateComponent);
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} not found in scene content");
                    continue;
                }

                loadingStateComponent.AssetHash = hash;

                // Supported formats https://docs.decentraland.org/creator/scene-editor/build/import-items#supported-formats
                if (path.EndsWith(".mp3", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".wav", StringComparison.InvariantCultureIgnoreCase)
                    || path.EndsWith(".ogg", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!AudioUtils.TryCreateAudioClipPromise(World, sceneData, path, PartitionComponent.MIN_PRIORITY, out AudioPromise? assetPromise))
                    {
                        MarkForUpdateAndStore(LoadingState.FinishedWithError, createdEntity, ref existingComponent, ref loadingStateComponent);
                        continue;
                    }

                    World.Add(createdEntity, assetPromise!.Value);
                }
                else if (path.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBVideoPlayer component = new PBVideoPlayer
                    {
                        Src = path,
                    };
                    World.Add(createdEntity, component);
                }
                else if (path.EndsWith(".png", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpg", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".jpeg", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!sceneData.TryGetContentUrl(path, out URLAddress contentUrl))
                    {
                        MarkForUpdateAndStore(LoadingState.NotFound, createdEntity, ref existingComponent, ref loadingStateComponent);
                        continue;
                    }

                    var promise = TexturePromise.Create(World,
                        new GetTextureIntention
                        {
                            CommonArguments = new CommonLoadingArguments(contentUrl),
                            ReportSource = GetReportCategory(),
                        },
                        PartitionComponent.MIN_PRIORITY);
                    World.Add(createdEntity, promise);
                }
                else if (path.EndsWith(".glTF", StringComparison.InvariantCultureIgnoreCase)
                         || path.EndsWith(".glb", StringComparison.InvariantCultureIgnoreCase))
                {
                    PBGltfContainer component = new PBGltfContainer
                    {
                        Src = path,
                    };
                    World.Add(createdEntity, component);
                }
                else
                {
                    ReportHub.LogWarning(GetReportData(), $"Asset {path} has unsupported format");
                    continue;
                }

                MarkForUpdateAndStore(LoadingState.Loading, createdEntity, ref existingComponent, ref loadingStateComponent);
            }
        }

        // Note: every loop iteration we are manipulating the World twice (adding the specific promise/component + adding here the loading state component)
        //       this works because we're passing the Entity here as value rather than a ref.
        private void MarkForUpdateAndStore(LoadingState loadingState, Entity entity, ref AssetPreLoadComponent existingComponent, ref AssetPreLoadLoadingStateComponent loadingStateComponent)
        {
            loadingStateComponent.LoadingState = loadingState;
            loadingStateComponent.IsDirty = true;

            World.Add(entity, loadingStateComponent);
            existingComponent.LoadingAssetPaths.Add(loadingStateComponent.AssetPath);
        }
    }
}
