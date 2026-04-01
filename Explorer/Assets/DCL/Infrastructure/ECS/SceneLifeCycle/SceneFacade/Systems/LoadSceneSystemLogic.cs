using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using SceneRunner.Scene;
using System.Linq;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private const string SCENE_JSON = "scene.json";
        private const string MAIN_CRDT = "main.crdt";

        private static readonly string[] CDN_FILE_NAMES = { SCENE_JSON, MAIN_CRDT };

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

            // Check main script + fixed CDN files in parallel with HEAD requests
            var headTasks = new UniTask<bool>[CDN_FILE_NAMES.Length + 1];

            headTasks[0] = webRequestController.IsHeadReachableAsync(reportCategory,
                                                    assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{mainFileName}")), ct)
                                               .SuppressAnyExceptionWithFallback(false);

            for (var i = 0; i < CDN_FILE_NAMES.Length; i++)
            {
                URLAddress cdnUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{CDN_FILE_NAMES[i]}"));
                headTasks[i + 1] = webRequestController.IsHeadReachableAsync(reportCategory, cdnUrl, ct).SuppressAnyExceptionWithFallback(false);
            }

            bool[] results = await UniTask.WhenAll(headTasks);

            if (results.All(r => r))
            {
                hashedContent.OverrideContentUrl(mainFileName, assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{mainFileName}")));

                for (var i = 0; i < CDN_FILE_NAMES.Length; i++)
                    hashedContent.OverrideContentUrl(CDN_FILE_NAMES[i], assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{CDN_FILE_NAMES[i]}")));
            }
        }
    }
}
