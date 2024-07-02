using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.WebRequests;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private readonly URLDomain assetBundleURL;
        private readonly IWebRequestController webRequestController;

        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL) : base(webRequestController, assetBundleURL)
        {
        }


        protected override string GetAssetBundleSceneId(string ipfsPathEntityId)
        {
            return ipfsPathEntityId;
        }

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, string reportCategory)
        {
            return new SceneHashedContent(definition.content!, contentBaseUrl);
        }
    }
}
