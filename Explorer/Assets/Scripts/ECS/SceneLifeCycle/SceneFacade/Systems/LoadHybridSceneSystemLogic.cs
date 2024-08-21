using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
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
        private static readonly URLDomain WORLDS_CONTENT_URL = URLDomain.FromString("https://worlds-content-server.decentraland.org/contents/");
        private readonly HybridSceneContentServer hybridSceneContentServer;
        private readonly URLDomain hybridSceneContentServerDomain;
        private readonly Vector2Int startParcel;
        private readonly string world;
        private HibridSceneHashedContent? hybridSceneHashedContent;
        private string? remoteSceneID;

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, HybridSceneParams hybridSceneParams,
            ICharacterDataPropagationUtility characterDataPropagationUtility, World globalWorld, Entity playerEntity) :
            base(webRequestController, characterDataPropagationUtility, assetBundleURL, globalWorld, playerEntity)
        {
            world = hybridSceneParams.World;
            hybridSceneContentServer = hybridSceneParams.HybridSceneContentServer;
            startParcel = hybridSceneParams.StartParcel;

            switch (hybridSceneContentServer)
            {
                case HybridSceneContentServer.Goerli:
                    hybridSceneContentServerDomain = GOERLI_CONTENT_URL;
                    break;
                case HybridSceneContentServer.Genesis:
                    hybridSceneContentServerDomain = GENESIS_CONTENT_URL;
                    break;
                case HybridSceneContentServer.World:
                    hybridSceneContentServerDomain = WORLDS_CONTENT_URL;
                    break;
            }
        }

        protected override string GetAssetBundleSceneId(string _) =>
            hybridSceneHashedContent!.remoteSceneID;

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, string reportCategory)
        {
            hybridSceneHashedContent = new HibridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL);

            if (await hybridSceneHashedContent.TryGetRemoteSceneIDAsync(hybridSceneContentServerDomain, hybridSceneContentServer, startParcel, world, reportCategory)) { await hybridSceneHashedContent.GetRemoteSceneDefinitionAsync(hybridSceneContentServerDomain, reportCategory); }

            return hybridSceneHashedContent;
        }
    }
}
