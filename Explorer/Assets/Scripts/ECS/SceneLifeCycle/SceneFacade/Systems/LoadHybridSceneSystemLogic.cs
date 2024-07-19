using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.WebRequests;
using Global.Dynamic;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadHybridSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private readonly HibridSceneContentServer hibridSceneContentServer;
        private readonly URLDomain hibridSceneContentServerDomain;
        private readonly Vector2Int startParcel;
        private HibridSceneHashedContent hibridSceneHashedContent;
        private string remoteSceneID;
        private readonly string world;

        private static readonly URLDomain GOERLI_CONTENT_URL = URLDomain.FromString("https://sdk-team-cdn.decentraland.org/ipfs/");
        private static readonly URLDomain GENESIS_CONTENT_URL = URLDomain.FromString("https://peer.decentraland.org/content/contents/");
        private static readonly URLDomain WORLDS_CONTENT_URL = URLDomain.FromString("https://worlds-content-server.decentraland.org/contents/");

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, HybridSceneParams hybridSceneParams) : base(webRequestController, assetBundleURL)
        {
            world = hybridSceneParams.World;
            hibridSceneContentServer = hybridSceneParams.HybridSceneContentServer;
            startParcel = hybridSceneParams.StartParcel;
            switch (hibridSceneContentServer)
            {
                case HibridSceneContentServer.Goerli:
                    hibridSceneContentServerDomain = GOERLI_CONTENT_URL;
                    break;
                case HibridSceneContentServer.Genesis:
                    hibridSceneContentServerDomain = GENESIS_CONTENT_URL;
                    break;
                case HibridSceneContentServer.World:
                    hibridSceneContentServerDomain = WORLDS_CONTENT_URL;
                    break;
            }
        }

        protected override string GetAssetBundleSceneId(string _)
        {
            return hibridSceneHashedContent.remoteSceneID;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, string reportCategory)
        {
            hibridSceneHashedContent = new HibridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL);
            if (await hibridSceneHashedContent.TryGetRemoteSceneIDAsync(hibridSceneContentServerDomain, hibridSceneContentServer, startParcel, world, reportCategory))
            {
                await hibridSceneHashedContent.GetRemoteSceneDefinitionAsync(hibridSceneContentServerDomain, reportCategory);
            }
            return hibridSceneHashedContent;
        }
    }
}