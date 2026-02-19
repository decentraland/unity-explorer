using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using Global.Dynamic;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadHybridSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private static readonly URLDomain GOERLI_CONTENT_URL = URLDomain.FromString("https://sdk-team-cdn.decentraland.org/ipfs/");
        private static readonly URLDomain GENESIS_CONTENT_URL = URLDomain.FromString("https://peer.decentraland.org/content/contents/");
        private readonly HybridSceneContentServer hybridSceneContentServer;
        private readonly URLDomain hybridSceneContentServerDomain;
        private readonly string world;
        private readonly string? worldContentServerBaseUrl;
        private HybridSceneHashedContent? hybridSceneHashedContent;
        private string? remoteSceneID;

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, HybridSceneParams hybridSceneParams, URLDomain worldContentServerContentsUrl, string worldContentServerBaseUrl) : base(webRequestController, assetBundleURL)
        {
            world = hybridSceneParams.World;
            hybridSceneContentServer = hybridSceneParams.HybridSceneContentServer;
            this.worldContentServerBaseUrl = worldContentServerBaseUrl;

            switch (hybridSceneContentServer)
            {
                case HybridSceneContentServer.Goerli:
                    hybridSceneContentServerDomain = GOERLI_CONTENT_URL;
                    break;
                case HybridSceneContentServer.Genesis:
                    hybridSceneContentServerDomain = GENESIS_CONTENT_URL;
                    break;
                case HybridSceneContentServer.World:
                    hybridSceneContentServerDomain = worldContentServerContentsUrl;
                    break;
            }
        }

        protected override string GetAssetBundleSceneId(string _) =>
            hybridSceneHashedContent!.remoteSceneID;

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory)
        {
            hybridSceneHashedContent = new HybridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL);

            if (await hybridSceneHashedContent.TryGetRemoteSceneIDAsync(hybridSceneContentServerDomain,
                    hybridSceneContentServer, definition.metadata.scene.DecodedBase, world, reportCategory, worldContentServerBaseUrl))
            {
                await hybridSceneHashedContent.GetRemoteSceneDefinitionAsync(hybridSceneContentServerDomain,
                    reportCategory);
            }

            return hybridSceneHashedContent;
        }
    }
}
