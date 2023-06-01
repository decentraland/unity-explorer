using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        private readonly Dictionary<string, string> fileToHash;

        private readonly Ipfs.SceneEntityDefinition sceneDefinition;

        private readonly string contentBaseUrl;

        private readonly bool supportHashes;

        private readonly List<Vector2Int> parcels = new List<Vector2Int>();

        private readonly Vector2Int baseParcel;

        public SceneData(string contentBaseUrl, Ipfs.SceneEntityDefinition sceneDefinition, bool supportHashes)
        {
            this.contentBaseUrl = contentBaseUrl;
            this.sceneDefinition = sceneDefinition;
            this.supportHashes = supportHashes;

            fileToHash = new Dictionary<string, string>(sceneDefinition.content.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var contentDefinition in sceneDefinition.content)
            {
                fileToHash[contentDefinition.file] = contentDefinition.hash;
            }

            baseParcel = Ipfs.DecodePointer(sceneDefinition.metadata.scene.baseParcel);

            foreach (string parcel in sceneDefinition.metadata.scene.parcels)
            {
                parcels.Add(Ipfs.DecodePointer(parcel));
            }
        }

        public string SceneName => sceneDefinition.id;
        public IReadOnlyList<Vector2Int> Parcels => parcels;
        public Vector2Int BaseParcel => baseParcel;

        public bool HasRequiredPermission(string permission)
        {
            return true;

            // TODO: Implement
            /*for (var i = 0; i < sceneData.RequiredPermissions.Count; i++)
            {
                if (sceneData.RequiredPermissions[i] == permission)
                    return true;
            }

            return false;*/
        }

        public bool TryGetMainScriptUrl(out string result) =>
            TryGetContentUrl(sceneDefinition.metadata.main, out result);

        public bool TryGetContentUrl(string url, out string result)
        {
            if (supportHashes)
            {
                if (fileToHash.TryGetValue(url, out string hash))
                {
                    result = contentBaseUrl + hash;
                    return true;
                }

                Debug.LogError($"{nameof(SceneData)}: {url} not found in {nameof(fileToHash)}");

                result = string.Empty;
                return false;
            }

            result = contentBaseUrl + url;
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
            return true;

            // TODO: Implement
            /*if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                for (var i = 0; i < sceneData.AllowedMediaHostnames.Count; i++)
                {
                    if (string.Equals(sceneData.AllowedMediaHostnames[i], uri.Host, StringComparison.CurrentCultureIgnoreCase))
                        return true;
                }
            }

            return false;*/
        }
    }
}
