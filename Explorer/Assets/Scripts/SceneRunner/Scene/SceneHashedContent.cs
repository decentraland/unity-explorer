using Diagnostics.ReportsHandling;
using Ipfs;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneHashedContent : ISceneContent
    {
        private readonly string contentBaseUrl;
        private readonly Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, (bool success, string url)> resolvedContentURLs;

        public SceneHashedContent(IReadOnlyList<IpfsTypes.ContentDefinition> contentDefinitions, string contentBaseUrl)
        {
            fileToHash = new Dictionary<string, string>(contentDefinitions.Count, StringComparer.OrdinalIgnoreCase);
            foreach (IpfsTypes.ContentDefinition contentDefinition in contentDefinitions) fileToHash[contentDefinition.file] = contentDefinition.hash;
            resolvedContentURLs = new Dictionary<string, (bool success, string url)>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string url, out string result)
        {
            if (resolvedContentURLs.TryGetValue(url, out (bool success, string url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (fileToHash.TryGetValue(url, out string hash))
            {
                result = contentBaseUrl + hash;
                resolvedContentURLs[url] = (true, result);
                return true;
            }

            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"{nameof(SceneHashedContent)}: {url} not found in {nameof(fileToHash)}");

            result = string.Empty;
            resolvedContentURLs[url] = (false, result);
            return false;
        }

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);
    }
}
