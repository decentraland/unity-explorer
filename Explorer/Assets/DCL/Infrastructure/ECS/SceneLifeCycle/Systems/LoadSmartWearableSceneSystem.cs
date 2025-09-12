using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.PluginSystem;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
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
    public struct SmartWearableScene
    {
        public SceneDefinitionComponent Definition;

        public ISceneFacade Scene;
    }

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

            SceneEntityDefinition sceneDefinition = await GetSceneDefinitionAsync(CONTENT_URL, intention.SmartWearable, ct);
            if (ct.IsCancellationRequested) return new StreamableLoadingResult<GetSmartWearableSceneIntention.Result>();

            var definitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneDefinition, new IpfsPath(sceneDefinition.id, URLDomain.EMPTY), true);
            var sceneContent = new SmartWearableSceneContent(URLDomain.FromString(CONTENT_URL), intention.SmartWearable);
            var assetBundleManifest = intention.SmartWearable.ManifestResult!.Value.Asset!;

            var sceneData = new SceneData(
                sceneContent,
                sceneDefinition,
                assetBundleManifest,
                sceneDefinition.metadata.scene.DecodedBase,
                definitionComponent.SceneGeometry,
                definitionComponent.Parcels,
                StaticSceneMessages.EMPTY); // TODO should be something like `new StaticSceneMessages(crdt));`

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

        private async Task<SceneEntityDefinition> GetSceneDefinitionAsync(string url, IWearable wearable, CancellationToken ct)
        {
            foreach (var content in wearable.DTO.content)
            {
                if (!content.file.EndsWith("scene.json", StringComparison.Ordinal)) continue;

                var args = new CommonLoadingArguments(URLAddress.FromString(url + content.hash));
                var sceneMetadata = await webRequestController.GetAsync(args, ct, GetReportData())
                                                              .CreateFromJson<SceneMetadata>(WRJsonParser.Newtonsoft, WRThreadFlags.SwitchToThreadPool);

                return new SceneEntityDefinition(content.hash, sceneMetadata);
            }

            throw new InvalidOperationException();
        }
    }
}
