using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneNonHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private readonly Dictionary<string, (bool success, URLAddress url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;

        public SceneNonHashedContent(URLDomain contentBaseUrl)
        {
            resolvedContentURLs = new Dictionary<string, (bool success, URLAddress url)>(StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress result)
        {
            if (resolvedContentURLs.TryGetValue(contentPath, out (bool success, URLAddress url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            result = contentBaseUrl.Append(URLPath.FromString(contentPath));
            resolvedContentURLs[contentPath] = (true, result);
            return true;
        }

        public bool TryGetHash(string name, out string hash)
        {
            hash = name;
            return true;
        }
    }
}
