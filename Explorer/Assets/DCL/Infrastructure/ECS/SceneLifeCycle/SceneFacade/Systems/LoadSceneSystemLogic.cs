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
        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL)
            : base(webRequestController, assetBundleURL) { }

        protected override string GetAssetBundleSceneId(string ipfsPathEntityId) =>
            ipfsPathEntityId;

        protected override UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory)
        {
            var hashedContent = new SceneHashedContent(definition.content!, contentBaseUrl);

            // When the scene has an asset bundle manifest, redirect the main script URL to the CDN
            // so the Explorer desktop client does not need to fetch it from the catalyst.
            // main.crdt has its own CDN-first logic with catalyst fallback in LoadMainCrdtAsync.
            if (definition.assetBundleManifestVersion != null
                && !definition.assetBundleManifestVersion.IsEmpty()
                && !string.IsNullOrEmpty(definition.metadata?.main))
            {
                string? abVersion = definition.assetBundleManifestVersion.GetAssetBundleManifestVersion();

                if (!string.IsNullOrEmpty(abVersion))
                {
                    URLAddress cdnMainScriptUrl = assetBundleURL.Append(URLPath.FromString($"{abVersion}/{definition.id}/index.js"));
                    return UniTask.FromResult<ISceneContent>(new SceneHashedContentWithCDN(hashedContent, definition.metadata.main, cdnMainScriptUrl));
                }
            }

            return UniTask.FromResult<ISceneContent>(hashedContent);
        }
    }
}
