using Ipfs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        private readonly Dictionary<string, string> fileToHash;

        private readonly IIpfsRealm ipfsRealm;

        private readonly List<Vector2Int> parcels = new ();

        private readonly IpfsTypes.SceneEntityDefinition sceneDefinition;

        private string customContentBaseUrl = null;

        private readonly bool supportHashes;

        private string GetContentBaseUrl()
        {
            if (customContentBaseUrl != null) { return customContentBaseUrl; }

            return ipfsRealm.ContentBaseUrl;
        }

        public SceneData(IIpfsRealm ipfsRealm, IpfsTypes.SceneEntityDefinition sceneDefinition, bool supportHashes, string customContentBaseUrl = null)
        {
            this.ipfsRealm = ipfsRealm;
            this.sceneDefinition = sceneDefinition;
            this.supportHashes = supportHashes;
            this.customContentBaseUrl = customContentBaseUrl;

            if (!supportHashes)
            {
                BaseParcel = new Vector2Int(0, 0);
                return;
            }

            fileToHash = new Dictionary<string, string>(sceneDefinition.content.Count, StringComparer.OrdinalIgnoreCase);

            foreach (IpfsTypes.ContentDefinition contentDefinition in sceneDefinition.content) { fileToHash[contentDefinition.file] = contentDefinition.hash; }

            foreach (string parcel in sceneDefinition.metadata.scene.parcels) { parcels.Add(IpfsHelper.DecodePointer(parcel)); }

            BaseParcel = IpfsHelper.DecodePointer(sceneDefinition.metadata.scene.baseParcel);
        }

        public string SceneName => sceneDefinition.id;
        public IReadOnlyList<Vector2Int> Parcels => parcels;
        public Vector2Int BaseParcel { get; }

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
            if (supportHashes)
            {
                if (fileToHash.TryGetValue(url, out string hash))
                {
                    result = GetContentBaseUrl() + hash;
                    return true;
                }

                Debug.LogError($"{nameof(SceneData)}: {url} not found in {nameof(fileToHash)}");

                result = string.Empty;
                return false;
            }

            result = GetContentBaseUrl() + url;
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
