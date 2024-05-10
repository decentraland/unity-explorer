using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadHibridSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private readonly string hibridSceneID;
        private readonly string hibridSceneContentServer;

        public LoadHibridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, string hibridSceneID, string hibridSceneContentServer) : base(webRequestController, assetBundleURL)
        {
            this.hibridSceneID = hibridSceneID;
            this.hibridSceneContentServer = hibridSceneContentServer;
        }

        public override async UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            var definitionComponent = intention.DefinitionComponent;
            var ipfsPath = definitionComponent.IpfsPath;
            var definition = definitionComponent.Definition;

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            var contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            ISceneContent hashedContent = new SceneHashedContent(definition.content, contentBaseUrl);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            var loadAssetBundleManifest = LoadAssetBundleManifestAsync(hibridSceneID, reportCategory, ct);
            var loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, ipfsPath.EntityId, reportCategory, ct);
            var loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            (var manifest, _, var mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Calculate partition immediately
            await UniTask.SwitchToMainThread();

            hashedContent = new HibridSceneHashedContent(webRequestController, definition.content, contentBaseUrl, assetBundleURL , URLDomain.FromString(hibridSceneContentServer), hibridSceneID);
            await ((HibridSceneHashedContent)hashedContent).GetRemoteSceneDefinition(new CancellationToken(), reportCategory);

            // Create scene data
            var baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, manifest, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Calculate partition immediately
            await UniTask.SwitchToMainThread();

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }
    }
}