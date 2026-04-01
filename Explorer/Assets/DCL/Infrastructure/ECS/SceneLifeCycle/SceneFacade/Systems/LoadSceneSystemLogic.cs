using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private const string SCENE_JSON = "scene.json";
        private const string MAIN_CRDT = "main.crdt";

        public LoadSceneSystemLogic(IWebRequestController webRequestController, URLDomain assetBundleURL)
            : base(webRequestController, assetBundleURL) { }

        protected override string GetAssetBundleSceneId(string ipfsPathEntityId) =>
            ipfsPathEntityId;

        protected override async UniTask<ISceneContent> GetSceneHashedContentAsync(SceneEntityDefinition definition, URLDomain contentBaseUrl, ReportData reportCategory, CancellationToken ct)
        {
            var hashedContent = new SceneHashedContent(definition.content!, contentBaseUrl);

            string? abVersion = definition.assetBundleManifestVersion?.GetAssetBundleManifestVersion();

            if (string.IsNullOrEmpty(abVersion) || string.IsNullOrEmpty(definition.metadata?.main))
                return hashedContent;

            await TryOverrideWithCDNAsync(hashedContent, definition, abVersion, reportCategory, ct);

            return hashedContent;
        }

        private async UniTask TryOverrideWithCDNAsync(SceneHashedContent hashedContent, SceneEntityDefinition definition, string abVersion, ReportData reportCategory, CancellationToken ct)
        {
            string mainFileName = definition.metadata.main;
            var cdnBasePath = $"{abVersion}/{definition.id}/";

            URLAddress mainUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{mainFileName}"));
            URLAddress sceneJsonUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{SCENE_JSON}"));
            URLAddress mainCrdtUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{MAIN_CRDT}"));

            // Check main script + fixed CDN files in parallel with HEAD requests
            (bool mainReachable, bool sceneJsonReachable, bool mainCrdtReachable) = await UniTask.WhenAll(
                webRequestController.IsHeadReachableAsync(reportCategory, mainUrl, ct).SuppressAnyExceptionWithFallback(false),
                webRequestController.IsHeadReachableAsync(reportCategory, sceneJsonUrl, ct).SuppressAnyExceptionWithFallback(false),
                webRequestController.IsHeadReachableAsync(reportCategory, mainCrdtUrl, ct).SuppressAnyExceptionWithFallback(false));

            if (mainReachable && sceneJsonReachable && mainCrdtReachable)
            {
                hashedContent.OverrideContentUrl(mainFileName, mainUrl);
                hashedContent.OverrideContentUrl(SCENE_JSON, sceneJsonUrl);
                hashedContent.OverrideContentUrl(MAIN_CRDT, mainCrdtUrl);
            }
        }
    }
}
