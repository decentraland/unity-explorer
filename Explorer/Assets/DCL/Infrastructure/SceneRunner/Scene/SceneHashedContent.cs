using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private readonly Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, (bool success, Uri url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;

        public SceneHashedContent(IReadOnlyList<ContentDefinition> contentDefinitions, URLDomain contentBaseUrl)
        {
            fileToHash = new Dictionary<string, string>(contentDefinitions.Count, StringComparer.OrdinalIgnoreCase);
            foreach (ContentDefinition contentDefinition in contentDefinitions) fileToHash[contentDefinition.file] = contentDefinition.hash;
            resolvedContentURLs = new Dictionary<string, (bool success, Uri url)>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string contentPath, out Uri result)
        {
            if (resolvedContentURLs.TryGetValue(contentPath, out (bool success, Uri url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (fileToHash.TryGetValue(contentPath, out string hash))
            {
                result = contentBaseUrl.Append(URLPath.FromString(hash));
                resolvedContentURLs[contentPath] = (true, result);
                return true;
            }

            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"{nameof(SceneHashedContent)}: {contentPath} not found in {nameof(fileToHash)}");

            result = null!;
            resolvedContentURLs[contentPath] = (false, result);
            return false;
        }

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);
    }
}
