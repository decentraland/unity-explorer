using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneNonHashedContent : ISceneContent
    {
        private readonly string contentBaseUrl;
        private readonly Dictionary<string, (bool success, string url)> resolvedContentURLs;

        public SceneNonHashedContent(string contentBaseUrl)
        {
            resolvedContentURLs = new Dictionary<string, (bool success, string url)>(StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string url, out string result)
        {
            if (resolvedContentURLs.TryGetValue(url, out (bool success, string url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            result = contentBaseUrl + url;
            resolvedContentURLs[url] = (true, result);
            return true;
        }

        public bool TryGetHash(string name, out string hash)
        {
            hash = name;
            return true;
        }
    }
}
