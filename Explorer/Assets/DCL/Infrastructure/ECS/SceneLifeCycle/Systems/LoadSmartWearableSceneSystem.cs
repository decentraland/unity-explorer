using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.PluginSystem;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner;
using SceneRunner.Scene;
using System;
using System.Threading;
using System.Threading.Tasks;

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

        public LoadSmartWearableSceneSystem(
            Arch.Core.World world,
            IStreamableCache<GetSmartWearableSceneIntention.Result, GetSmartWearableSceneIntention> cache,
            IWebRequestController webRequestController,
            ISceneFactory sceneFactory) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.sceneFactory = sceneFactory;
        }

        protected override async UniTask<StreamableLoadingResult<GetSmartWearableSceneIntention.Result>> FlowInternalAsync(GetSmartWearableSceneIntention intention, StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            const string CONTENT_URL = "https://peer.decentraland.org/content/contents/";

            var player = World.CachePlayer();
            var avatarShape = World.Get<AvatarShapeComponent>(player);
            var bodyShape = avatarShape.BodyShape;

            var sceneContent = SmartWearableSceneContent.Create(URLDomain.FromString(CONTENT_URL), intention.SmartWearable, bodyShape);

            SceneEntityDefinition? sceneDefinition = await GetSceneDefinitionAsync(intention.SmartWearable.DTO.id, sceneContent, ct);
            if (sceneDefinition == null || ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

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

            ISceneFacade? sceneFacade = await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
            if (ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

            await UniTask.SwitchToMainThread();
            sceneFacade?.Initialize();

            ReportHub.Log(GetReportCategory(), $"Smart Wearable scene {intention.SmartWearable.DTO.id} loaded");

            var result = new GetSmartWearableSceneIntention.Result
            {
                SceneDefinition = definitionComponent,
                SceneFacade = sceneFacade
            };
            return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>(result);
        }

        private async Task<SceneEntityDefinition?> GetSceneDefinitionAsync(string sceneId, ISceneContent sceneContent, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl("scene.json", out URLAddress url))
            {
                ReportHub.LogError(GetReportCategory(), "Could not find 'scene.json'");
                return null;
            }

            var args = new CommonLoadingArguments(URLAddress.FromString(url));
            var sceneMetadata = await webRequestController.GetAsync(args, ct, GetReportData())
                                                          .CreateFromJson<SceneMetadata>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

            return new SceneEntityDefinition(sceneId, sceneMetadata);
        }

        private async UniTask<ReadOnlyMemory<byte>> GetCrdtAsync(ISceneContent sceneContent, CancellationToken ct)
        {
            if (!sceneContent.TryGetContentUrl("assets/scene/main.composite", out var url))
            {
                ReportHub.LogError(GetReportCategory(), "Could not find 'main.composite'");
                return ReadOnlyMemory<byte>.Empty;
            }

            return await webRequestController.GetAsync(new CommonArguments(url), ct, GetReportData()).GetDataCopyAsync();
        }
    }
}
