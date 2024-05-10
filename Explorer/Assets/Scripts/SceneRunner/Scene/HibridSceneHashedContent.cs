using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;

namespace SceneRunner.Scene
{
    /// Used for localhost scenes that require remote asset bundle content
    public class HibridSceneHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, (bool success, URLAddress url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;
        private readonly IWebRequestController webRequestController;
        private readonly string remoteSceneID;

        private readonly URLDomain abDomain;
        private readonly URLDomain remoteContentDomain;


        private readonly string[] filesToGetFromLocalHost =
        {
            "bin/index.js", "scene.json"
        };

        public HibridSceneHashedContent(IWebRequestController webRequestController,
            IReadOnlyList<ContentDefinition> contentDefinitions, URLDomain contentBaseUrl,
            URLDomain abDomain, URLDomain remoteContentDomain,
            string remoteSceneID)
        {
            fileToHash = new Dictionary<string, string>(contentDefinitions.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var contentDefinition in contentDefinitions) fileToHash[contentDefinition.file] = contentDefinition.hash;
            resolvedContentURLs = new Dictionary<string, (bool success, URLAddress url)>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
            this.abDomain = abDomain;
            this.webRequestController = webRequestController;
            this.remoteSceneID = remoteSceneID;
            this.remoteContentDomain = remoteContentDomain;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            if (resolvedContentURLs.TryGetValue(contentPath, out var cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (fileToHash.TryGetValue(contentPath, out string hash))
            {
                if (filesToGetFromLocalHost.Contains(contentPath))
                {
                    result = contentBaseUrl.Append(URLPath.FromString(hash));
                    resolvedContentURLs[contentPath] = (true, result);
                    return true;
                }

                result = abDomain.Append(URLPath.FromString(hash));
                resolvedContentURLs[contentPath] = (true, result);
                return true;
            }

            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"{nameof(SceneHashedContent)}: {contentPath} not found in {nameof(fileToHash)}");

            result = URLAddress.EMPTY;
            resolvedContentURLs[contentPath] = (false, result);
            return false;
        }

        public bool TryGetHash(string name, out string hash)
        {
            return fileToHash.TryGetValue(name, out hash);
        }

        public async UniTask GetRemoteSceneDefinition(CancellationToken ct, string reportCategory)
        {
            var url = remoteContentDomain.Append(URLPath.FromString(remoteSceneID));

            try
            {
                var sceneEntityDefinition = await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory)
                    .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                foreach (var contentDefinition in sceneEntityDefinition.content)
                {
                    if (fileToHash.ContainsKey(contentDefinition.file) && !filesToGetFromLocalHost.Contains(contentDefinition.file))
                        fileToHash[contentDefinition.file] = contentDefinition.hash;
                }
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with id {remoteSceneID} failed. You wont get the asset bundles");
            }

        }
    }
}