using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using Google.Protobuf;
using Ipfs;
using System;
using System.Collections.Generic;

namespace SceneRunner.Scene
{
    public class SceneHashedContent : ISceneContent
    {
        private struct ContentAccessResult
        {
            public bool Success;
            public URLAddress URL;
            public string FileHash;

            public ContentAccessResult(bool success, URLAddress url, string fileHash)
            {
                Success = success;
                URL = url;
                FileHash = fileHash;
            }
        }

        private readonly URLDomain contentBaseUrl;
        private readonly Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, ContentAccessResult> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;

        public SceneHashedContent(IReadOnlyList<ContentDefinition> contentDefinitions, URLDomain contentBaseUrl)
        {
            fileToHash = new Dictionary<string, string>(contentDefinitions.Count, StringComparer.OrdinalIgnoreCase);
            foreach (ContentDefinition contentDefinition in contentDefinitions) fileToHash[contentDefinition.file] = contentDefinition.hash;
            resolvedContentURLs = new Dictionary<string, ContentAccessResult>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);
            this.contentBaseUrl = contentBaseUrl;
        }

        public bool TryGetContentUrl(string contentPath, out URLAddress resultURL, out string fileHash)
        {
            if (resolvedContentURLs.TryGetValue(contentPath, out ContentAccessResult cachedResult))
            {
                resultURL = cachedResult.URL;
                fileHash = cachedResult.FileHash;
                return cachedResult.Success;
            }

            if (fileToHash.TryGetValue(contentPath, out string hash))
            {
                resultURL = contentBaseUrl.Append(URLPath.FromString(hash));
                fileHash = hash;
                resolvedContentURLs[contentPath] = new ContentAccessResult(true, resultURL, fileHash);
                return true;
            }

            ReportHub.LogWarning(ReportCategory.SCENE_LOADING, $"{nameof(SceneHashedContent)}: {contentPath} not found in {nameof(fileToHash)}");

            resultURL = URLAddress.EMPTY;
            fileHash = string.Empty;
            resolvedContentURLs[contentPath] = new ContentAccessResult(false, resultURL, fileHash);
            return false;
        }

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);
    }
}
