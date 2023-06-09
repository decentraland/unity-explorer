using Diagnostics;
using Ipfs;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        private readonly Dictionary<string, string> fileToHash;

        private readonly IIpfsRealm ipfsRealm;

        private readonly IpfsTypes.SceneEntityDefinition sceneDefinition;

        private readonly string customContentBaseUrl;

        private readonly bool supportHashes;

        private readonly Dictionary<string, (bool success, string url)> resolvedContentURLs;

        private string GetContentBaseUrl() =>
            customContentBaseUrl ?? ipfsRealm.ContentBaseUrl;

        public SceneData(IIpfsRealm ipfsRealm, IpfsTypes.SceneEntityDefinition sceneDefinition, bool supportHashes, [NotNull] SceneAssetBundleManifest assetBundleManifest, string customContentBaseUrl = null)
        {
            this.ipfsRealm = ipfsRealm;
            this.sceneDefinition = sceneDefinition;
            this.supportHashes = supportHashes;
            this.customContentBaseUrl = customContentBaseUrl;
            AssetBundleManifest = assetBundleManifest;

            if (!supportHashes)
            {
                resolvedContentURLs = new Dictionary<string, (bool success, string url)>(StringComparer.OrdinalIgnoreCase);
                SceneShortInfo = new SceneShortInfo(Vector2Int.zero, sceneDefinition.id);
                return;
            }

            fileToHash = new Dictionary<string, string>(sceneDefinition.content.Count, StringComparer.OrdinalIgnoreCase);

            foreach (IpfsTypes.ContentDefinition contentDefinition in sceneDefinition.content) fileToHash[contentDefinition.file] = contentDefinition.hash;

            SceneShortInfo = new SceneShortInfo(IpfsHelper.DecodePointer(sceneDefinition.metadata.scene.baseParcel), sceneDefinition.id);

            resolvedContentURLs = new Dictionary<string, (bool success, string url)>(fileToHash.Count, StringComparer.OrdinalIgnoreCase);
        }

        public SceneShortInfo SceneShortInfo { get; }
        public SceneAssetBundleManifest AssetBundleManifest { get; }

        public bool HasRequiredPermission(string permission)
        {
            if (sceneDefinition.metadata.scene.requiredPermissions == null)
                return false;

            foreach (string requiredPermission in sceneDefinition.metadata.scene.requiredPermissions)
            {
                if (requiredPermission == permission)
                    return true;
            }

            return false;
        }

        public bool TryGetMainScriptUrl(out string result) =>
            TryGetContentUrl(sceneDefinition.metadata.main, out result);

        public bool TryGetContentUrl(string url, out string result)
        {
            if (resolvedContentURLs.TryGetValue(url, out (bool success, string url) cachedResult))
            {
                result = cachedResult.url;
                return cachedResult.success;
            }

            if (supportHashes)
            {
                if (fileToHash.TryGetValue(url, out string hash))
                {
                    result = GetContentBaseUrl() + hash;
                    resolvedContentURLs[url] = (true, result);
                    return true;
                }

                Debug.LogError($"{nameof(SceneData)}: {url} not found in {nameof(fileToHash)}");

                result = string.Empty;
                resolvedContentURLs[url] = (false, result);
                return false;
            }

            result = ipfsRealm.ContentBaseUrl + url;
            resolvedContentURLs[url] = (true, result);
            return true;
        }

        public bool TryGetHash(string name, out string hash)
        {
            if (supportHashes)
                return fileToHash.TryGetValue(name, out hash);

            hash = name;
            return true;
        }

        public bool TryGetMediaUrl(string url, out string result)
        {
            if (string.IsNullOrEmpty(url))
            {
                result = string.Empty;
                return false;
            }

            // Try resolve an internal URL
            if (TryGetContentUrl(url, out result))
                return true;

            if (HasRequiredPermission(ScenePermissionNames.ALLOW_MEDIA_HOSTNAMES) && IsUrlDomainAllowed(url))
            {
                result = url;
                return true;
            }

            result = string.Empty;
            return false;
        }

        public bool IsUrlDomainAllowed(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                foreach (string allowedMediaHostname in sceneDefinition.metadata.scene.allowedMediaHostnames)
                {
                    if (string.Equals(allowedMediaHostname, uri.Host, StringComparison.CurrentCultureIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
