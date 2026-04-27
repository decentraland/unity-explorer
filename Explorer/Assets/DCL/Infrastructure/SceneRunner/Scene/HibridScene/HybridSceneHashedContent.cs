using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using UnityEngine;
using UnityEngine.Pool;

namespace SceneRunner.Scene
{
    /// Used for localhost scenes that require remote asset bundle content
    public class HybridSceneHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private readonly Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, (bool success, URLAddress url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;
        public string? remoteSceneID;

        private readonly IWebRequestController webRequestController;

        private readonly URLDomain abDomain;

        private readonly HashSet<string> filesToGetFromLocalHost = new (StringComparer.OrdinalIgnoreCase)
        {
            "scene.json", "main.crdt"
        };

        public HybridSceneHashedContent(IWebRequestController webRequestController,
            SceneEntityDefinition contentDefinitions, URLDomain contentBaseUrl,
            URLDomain abDomain)
        {
            filesToGetFromLocalHost.Add(contentDefinitions.metadata.main);
            fileToHash = new Dictionary<string, string>(contentDefinitions.content.Length, StringComparer.OrdinalIgnoreCase);
            foreach (var contentDefinition in contentDefinitions.content) fileToHash[contentDefinition.file] = contentDefinition.hash;
            resolvedContentURLs = new Dictionary<string, (bool success, URLAddress url)>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);

            this.contentBaseUrl = contentBaseUrl;
            this.abDomain = abDomain;
            this.webRequestController = webRequestController;
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
                //Textures are not fetched by asset bundles
                if (filesToGetFromLocalHost.Contains(contentPath) || IsNonConvertedAsset(contentPath))
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

        public bool TryGetHash(string name, out string hash) =>
            fileToHash.TryGetValue(name, out hash);

        public bool IsRawAsset(string contentPath) =>
            filesToGetFromLocalHost.Contains(contentPath) || IsNonConvertedAsset(contentPath);

        public async UniTask GetRemoteSceneDefinitionAsync(URLDomain remoteContentDomain, ReportData reportCategory, CancellationToken ct)
        {
            if (remoteSceneID == null)
                throw new ArgumentNullException(nameof(remoteSceneID));

            var url = remoteContentDomain.Append(URLPath.FromString(remoteSceneID));

            try
            {
                var sceneEntityDefinition = await webRequestController.GetAsync(new CommonArguments(url), ct, reportCategory)
                                                                      .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                var remoteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var contentDefinition in sceneEntityDefinition.content)
                {
                    remoteFiles.Add(contentDefinition.file);

                    if (fileToHash.ContainsKey(contentDefinition.file)
                        && !filesToGetFromLocalHost.Contains(contentDefinition.file)
                        && !IsNonConvertedAsset(contentDefinition.file))
                        fileToHash[contentDefinition.file] = contentDefinition.hash;
                }

                // Files that exist locally but not in the remote definition have no AB on the CDN — load from localhost
                foreach (string localFile in fileToHash.Keys)
                    if (!remoteFiles.Contains(localFile))
                        filesToGetFromLocalHost.Add(localFile);

                HashSetPool<string>.Release(remoteFiles);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with id {remoteSceneID} failed. You wont get the asset bundles"); }
        }

        /// <summary>
        ///     Files that are not converted to asset bundles and should be fetched from the local content server.
        /// </summary>
        private static bool IsNonConvertedAsset(string file)
        {
            return file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                   file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
        }

        public async UniTask<bool> TryGetRemoteSceneIDAsync(URLDomain contentDomain, HybridSceneContentServer remoteContentServer, Vector2Int coordinate, string world, ReportData reportCategory,
            CancellationToken ct, string? worldContentServerBaseUrl = null)
        {
            IGetHash getHash;

            switch (remoteContentServer)
            {
                case HybridSceneContentServer.Genesis:
                    getHash = new GetHashGenesis();
                    break;
                case HybridSceneContentServer.Goerli:
                    getHash = new GetHashGoerli();
                    break;
                case HybridSceneContentServer.World:
                    getHash = new GetHashWorld(world, worldContentServerBaseUrl);
                    break;
                default:
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Unexistent remote content domain {remoteContentServer.ToString()}");
                    return false;
            }

            (bool success, string sceneHash) = await getHash.TryGetHashAsync(webRequestController, contentDomain, coordinate, reportCategory, ct);

            if (success)
            {
                remoteSceneID = sceneHash;
                return true;
            }

            return false;
        }
    }
}
