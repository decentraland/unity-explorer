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
        private readonly string hibridSceneID;
        private readonly string hibridSceneContentServer;

        public LoadHybridSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, string hibridSceneID, string hibridSceneContentServer) : base(webRequestController, assetBundleURL)
        {
            this.hibridSceneID = hibridSceneID;
            this.hibridSceneContentServer = hibridSceneContentServer;
        }

        protected override string GetAssetBundleSceneId(string _)
        {
            return hibridSceneID;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(List<ContentDefinition>? definition, URLDomain contentBaseUrl, string reportCategory)
        {
            var hibridSceneHashedContent = new HibridSceneHashedContent(webRequestController, definition, contentBaseUrl, assetBundleURL , URLDomain.FromString(hibridSceneContentServer), hibridSceneID);
            await hibridSceneHashedContent.GetRemoteSceneDefinitionAsync(new CancellationToken(), reportCategory);
            return hibridSceneHashedContent;
        }
    }
}