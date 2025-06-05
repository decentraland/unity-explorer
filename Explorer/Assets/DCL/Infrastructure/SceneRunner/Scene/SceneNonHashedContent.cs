using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneNonHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private readonly Dictionary<string, (bool success, Uri url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;

        public SceneNonHashedContent(URLDomain contentBaseUrl)
        {
            resolvedContentURLs = new Dictionary<string, (bool success, Uri url)>(StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string contentPath, out Uri result)
        {
            if (resolvedContentURLs.TryGetValue(contentPath, out (bool success, Uri url) cachedResult))
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
