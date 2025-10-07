using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using Runtime.Wearables;
using SceneRunner;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    /// Handles the <see cref="GetSmartWearableSceneIntention"/> to load the actual scene associated with the smart wearable.
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadSmartWearableSceneSystem : LoadSystemBase<GetSmartWearableSceneIntention.Result, GetSmartWearableSceneIntention>
    {
        private readonly ISceneFactory sceneFactory;
        private readonly IWebRequestController webRequestController;
        private readonly SmartWearableCache smartWearableCache;

        public LoadSmartWearableSceneSystem(
            Arch.Core.World world,
            IStreamableCache<GetSmartWearableSceneIntention.Result, GetSmartWearableSceneIntention> cache,
            IWebRequestController webRequestController,
            ISceneFactory sceneFactory,
            SmartWearableCache smartWearableCache) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.sceneFactory = sceneFactory;
            this.smartWearableCache = smartWearableCache;
        }

        protected override async UniTask<StreamableLoadingResult<GetSmartWearableSceneIntention.Result>> FlowInternalAsync(GetSmartWearableSceneIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            IWearable wearable = intention.SmartWearable;

            (ISceneContent? sceneContent, SceneMetadata? sceneMetadata) = await smartWearableCache.GetCachedSceneInfoAsync(wearable, ct);
            if (ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

            AssetBundleManifestVersion manifestVersion = wearable.DTO.assetBundleManifestVersion!;
            SceneEntityDefinition sceneDefinition = new SceneEntityDefinition(wearable.DTO.id!, sceneMetadata) { assetBundleManifestVersion = manifestVersion };

            ReadOnlyMemory<byte> crdt = await GetCrdtAsync(sceneContent, ct);
            if (ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

            var definitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneDefinition, new IpfsPath(sceneDefinition.id!, URLDomain.EMPTY), true);

            var sceneData = new SceneData(
                sceneContent,
                sceneDefinition,
                sceneDefinition.metadata.scene.DecodedBase,
                definitionComponent.SceneGeometry,
                definitionComponent.Parcels,
                new StaticSceneMessages(crdt));

            var requiredPermissions = sceneDefinition.metadata.requiredPermissions;
            var scenePermissions = new RestrictedJsApiPermissionsProvider(requiredPermissions);
            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, scenePermissions, partition, ct);
            if (ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

            await UniTask.SwitchToMainThread();
            sceneFacade?.Initialize();

            ReportHub.Log(GetReportCategory(), $"Smart Wearable scene {SmartWearableCache.GetCacheId(wearable)} loaded");

            var result = new GetSmartWearableSceneIntention.Result
            {
                SceneDefinition = definitionComponent,
                SceneFacade = sceneFacade
            };
            return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>(result);
        }

        private async UniTask<ReadOnlyMemory<byte>> GetCrdtAsync(ISceneContent sceneContent, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl("main.crdt", out var url))
            {
                // We do not report any error since the 'main.crdt' file is not mandatory
                return ReadOnlyMemory<byte>.Empty;
            }

            return await webRequestController.GetAsync(new CommonArguments(url), ct, GetReportData()).GetDataCopyAsync();
        }
    }
}
