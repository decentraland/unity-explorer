using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.WebRequests;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadHybridSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private string hibridSceneID;
        private readonly string hibridSceneContentServer;

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, string hibridSceneContentServer) : base(webRequestController, assetBundleURL)
        {
            this.hibridSceneContentServer = hibridSceneContentServer;
        }

        protected override string GetAssetBundleSceneId(string _)
        {
            return hibridSceneID;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, string reportCategory)
        {
            var hibridSceneHashedContent = new HibridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL , URLDomain.FromString(hibridSceneContentServer), hibridSceneID);
            hibridSceneID = await hibridSceneHashedContent.GetRemoteSceneDefinitionAsync(definition.metadata.scene.DecodedBase, new CancellationToken(), reportCategory);
            return hibridSceneHashedContent;
        }
    }
}