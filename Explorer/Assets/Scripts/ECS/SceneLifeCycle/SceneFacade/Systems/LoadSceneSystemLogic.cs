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
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL) : base(webRequestController, assetBundleURL)
        {
        }

        public override async UniTask<ISceneFacade> FlowAsync(ISceneFactory sceneFactory, GetSceneFacadeIntention intention, string reportCategory, IPartitionComponent partition, CancellationToken ct)
        {
            SceneDefinitionComponent definitionComponent = intention.DefinitionComponent;
            IpfsPath ipfsPath = definitionComponent.IpfsPath;
            SceneEntityDefinition definition = definitionComponent.Definition;

            // Warning! Obscure Logic!
            // Each scene can override the content base url, so we need to check if the scene definition has a base url
            // and if it does, we use it, otherwise we use the realm's base url
            URLDomain contentBaseUrl = ipfsPath.BaseUrl.IsEmpty
                ? intention.IpfsRealm.ContentBaseUrl
                : ipfsPath.BaseUrl;

            var hashedContent = new SceneHashedContent(definition.content, contentBaseUrl);

            // Before a scene can be ever loaded the asset bundle manifest should be retrieved
            var loadAssetBundleManifest = LoadAssetBundleManifestAsync(ipfsPath.EntityId, reportCategory, ct);
            UniTask<UniTaskVoid> loadSceneMetadata = OverrideSceneMetadataAsync(hashedContent, intention, ipfsPath.EntityId, reportCategory, ct);
            UniTask<ReadOnlyMemory<byte>> loadMainCrdt = LoadMainCrdtAsync(hashedContent, reportCategory, ct);

            (SceneAssetBundleManifest manifest, _, ReadOnlyMemory<byte> mainCrdt) = await UniTask.WhenAll(loadAssetBundleManifest, loadSceneMetadata, loadMainCrdt);

            // Create scene data
            Vector2Int baseParcel = intention.DefinitionComponent.Definition.metadata.scene.DecodedBase;
            var sceneData = new SceneData(hashedContent, definitionComponent.Definition, manifest, baseParcel,
                definitionComponent.SceneGeometry, definitionComponent.Parcels, new StaticSceneMessages(mainCrdt));

            // Launch at the end of the frame
            await UniTask.SwitchToMainThread(PlayerLoopTiming.LastPostLateUpdate, ct);

            return await sceneFactory.CreateSceneFromSceneDefinition(sceneData, partition, ct);
        }

        
    }
}
