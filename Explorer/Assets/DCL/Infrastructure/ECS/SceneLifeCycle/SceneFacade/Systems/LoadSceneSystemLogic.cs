using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using SceneRunner.Scene;
using System;
using System.Threading;

namespace ECS.SceneLifeCycle.Systems
{
    public class LoadSceneSystemLogic : LoadSceneSystemLogicBase
    {
        private static readonly string[] CDN_FILE_NAMES = { "index.js", "scene.json", "main.crdt" };

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

            var cdnBasePath = $"{abVersion}/{definition.id}/";

            // Check all three CDN files in parallel with HEAD requests
            var headTasks = new UniTask<bool>[CDN_FILE_NAMES.Length];

            for (var i = 0; i < CDN_FILE_NAMES.Length; i++)
            {
                URLAddress cdnUrl = assetBundleURL.Append(URLPath.FromString($"{cdnBasePath}{CDN_FILE_NAMES[i]}"));
                headTasks[i] = CheckCdnFileExistsAsync(cdnUrl, reportCategory, ct);
            }

            bool[] results = await UniTask.WhenAll(headTasks);

            if (results[0] && results[1] && results[2])
                return new SceneHashedContentWithCDN(hashedContent, definition.metadata.main, assetBundleURL, cdnBasePath);

            return hashedContent;
        }

        private async UniTask<bool> CheckCdnFileExistsAsync(URLAddress url, ReportData reportCategory, CancellationToken ct)
        {
            try
            {
                int statusCode = await webRequestController
                                      .HeadAsync(new CommonArguments(url, RetryPolicy.NONE), ct, reportCategory)
                                      .StatusCodeAsync();

                return statusCode >= 200 && statusCode < 300;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
