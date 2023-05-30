using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class SceneContentProvider : ISceneContentProvider
    {
        private readonly Dictionary<string, string> fileToHash;

        private readonly ISceneData sceneData;
        private readonly bool supportHashes;

        /// <param name="sceneData"></param>
        /// <param name="supportHashes">
        ///     Indicates if hashes should be used for resources loading.
        ///     To load from local files it should be set to false
        /// </param>
        public SceneContentProvider(ISceneData sceneData, bool supportHashes)
        {
            this.sceneData = sceneData;
            this.supportHashes = supportHashes;

            if (!supportHashes) return;

            fileToHash = new Dictionary<string, string>(sceneData.Contents.Count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < sceneData.Contents.Count; i++)
            {
                ISceneData.ContentMappingPair mapping = sceneData.Contents[i];
                fileToHash[mapping.file] = mapping.hash;
            }
        }

        public string SceneName => sceneData.Name;

        public bool HasRequiredPermission(string permission)
        {
            for (var i = 0; i < sceneData.RequiredPermissions.Count; i++)
            {
                if (sceneData.RequiredPermissions[i] == permission)
                    return true;
            }

            return false;
        }

        public bool TryGetContentUrl(string url, out string result)
        {
            if (supportHashes)
            {
                if (fileToHash.TryGetValue(url, out string hash))
                {
                    result = sceneData.BaseUrl + hash;
                    return true;
                }

                Debug.LogError($"{nameof(SceneContentProvider)}: {url} not found in {nameof(fileToHash)}");

                result = string.Empty;
                return false;
            }

            result = sceneData.BaseUrl + url;
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
                for (var i = 0; i < sceneData.AllowedMediaHostnames.Count; i++)
                {
                    if (string.Equals(sceneData.AllowedMediaHostnames[i], uri.Host, StringComparison.CurrentCultureIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
