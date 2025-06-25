using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL, bool isLocalSceneDevelopment)
            : base(webRequestController, assetBundleURL, isLocalSceneDevelopment) { }

        protected override string GetAssetBundleSceneId(string ipfsPathEntityId) =>
            ipfsPathEntityId;

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory) =>
            new SceneHashedContent(definition.content!, contentBaseUrl);
    }
}
