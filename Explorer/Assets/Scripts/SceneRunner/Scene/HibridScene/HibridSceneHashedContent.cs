using System;
using System.Collections.Generic;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using UnityEngine;

namespace SceneRunner.Scene
{
    /// Used for localhost scenes that require remote asset bundle content
    public class HibridSceneHashedContent : ISceneContent
    {
        private readonly URLDomain contentBaseUrl;
        private Dictionary<string, string> fileToHash;
        private readonly Dictionary<string, (bool success, URLAddress url)> resolvedContentURLs;

        public URLDomain ContentBaseUrl => contentBaseUrl;
        public string remoteSceneID;

        private readonly IWebRequestController webRequestController;

        private readonly URLDomain abDomain;


        private readonly List<string> filesToGetFromLocalHost = new()
        {
            "scene.json", "main.crdt"
        };

        public HibridSceneHashedContent(IWebRequestController webRequestController,
            SceneEntityDefinition contentDefinitions, URLDomain contentBaseUrl,
            URLDomain abDomain)
        {
            filesToGetFromLocalHost.Add(contentDefinitions.metadata.main);
            fileToHash = new Dictionary<string, string>(contentDefinitions.content!.Count, StringComparer.OrdinalIgnoreCase);
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
                if (filesToGetFromLocalHost.Contains(contentPath) || IsTexture(contentPath))
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

        public async UniTask GetRemoteSceneDefinitionAsync(URLDomain remoteContentDomain, string reportCategory)
        {
            var url = remoteContentDomain.Append(URLPath.FromString(remoteSceneID));

            try
            {
                var sceneEntityDefinition = await webRequestController.GetAsync(new CommonArguments(url), new CancellationToken(), reportCategory)
                    .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

                foreach (var contentDefinition in sceneEntityDefinition.content)
                {
                    if (fileToHash.ContainsKey(contentDefinition.file) && !filesToGetFromLocalHost.Contains(contentDefinition.file) && !IsTexture(contentDefinition.file))
                        fileToHash[contentDefinition.file] = contentDefinition.hash;
                }
            }
            catch (Exception e)
            {
                ReportHub.LogError(reportCategory, $"Trying to load hybrid scene with id {remoteSceneID} failed. You wont get the asset bundles");
            }

        }

        private bool IsTexture(string contentDefinitionFile)
        {
            return contentDefinitionFile.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                   contentDefinitionFile.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase) ||
                   contentDefinitionFile.EndsWith("png", StringComparison.OrdinalIgnoreCase);
        }

        public async UniTask<bool> TryGetRemoteSceneIDAsync(URLDomain contentDomain, HybridSceneContentServer remoteContentServer, Vector2Int coordinate, string world, string reportCategory)
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
                    getHash = new GetHashWorld(world);
                    break;
                default:
                    ReportHub.LogError(ReportCategory.SCENE_LOADING, $"Unexistent remote content domain {remoteContentServer.ToString()}");
                    return false;
            }

            (bool success, string sceneHash) = await getHash.TryGetHashAsync(webRequestController, contentDomain, coordinate, reportCategory);
            if (success)
            {
                remoteSceneID = sceneHash;
                return true;
            }

            return false;
        }
    }
}