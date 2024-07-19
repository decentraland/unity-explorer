using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.WebRequests;
using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL,
            ICharacterDataPropagationUtility characterDataPropagationUtility, World globalWorld, Entity playerEntity)
            : base(webRequestController, characterDataPropagationUtility, assetBundleURL, globalWorld, playerEntity) { }

        protected override string GetAssetBundleSceneId(string ipfsPathEntityId) =>
            ipfsPathEntityId;

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, string reportCategory) =>
            new SceneHashedContent(definition.content!, contentBaseUrl);
    }
}
