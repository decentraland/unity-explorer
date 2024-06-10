using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        /// <summary>
        ///     https://github.com/decentraland/unity-renderer/pull/5844
        /// </summary>
        private const bool CHECK_ALLOWED_MEDIA_HOSTNAMES =
#if CHECK_ALLOWED_MEDIA_HOSTNAMES
            true;
#else
            false;
#endif

        public ISceneContent SceneContent { get; }

        public SceneEntityDefinition SceneEntityDefinition { get; }

        public StaticSceneMessages StaticSceneMessages { get; }
        public bool SceneLoadingConcluded { get; set; }
        public SceneShortInfo SceneShortInfo { get; }
        public ParcelMathHelper.SceneGeometry Geometry { get; }
        public SceneAssetBundleManifest AssetBundleManifest { get; }
        public IReadOnlyList<Vector2Int> Parcels { get; }

        public SceneData(
            ISceneContent sceneContent,
            SceneEntityDefinition sceneDefinition,
            [NotNull] SceneAssetBundleManifest assetBundleManifest,
            Vector2Int baseParcel,
            ParcelMathHelper.SceneGeometry geometry,
            IReadOnlyList<Vector2Int> parcels,
            StaticSceneMessages staticSceneMessages)
        {
            this.SceneContent = sceneContent;
            this.SceneEntityDefinition = sceneDefinition;
            AssetBundleManifest = assetBundleManifest;
            StaticSceneMessages = staticSceneMessages;
            Parcels = parcels;
            SceneShortInfo = new SceneShortInfo(baseParcel, sceneDefinition.id);
            Geometry = geometry;
        }

        public bool HasRequiredPermission(string permission)
        {
            if (SceneEntityDefinition.metadata.requiredPermissions == null)
                return false;

            foreach (string requiredPermission in SceneEntityDefinition.metadata.requiredPermissions)
            {
                if (requiredPermission == permission)
                    return true;
            }

            return false;
        }

        public bool TryGetMainScriptUrl(out URLAddress result) =>
            TryGetContentUrl(SceneEntityDefinition.metadata.main, out result);

        public bool TryGetContentUrl(string url, out URLAddress result) =>
            SceneContent.TryGetContentUrl(url, out result);

        public bool TryGetHash(string name, out string hash) =>
            SceneContent.TryGetHash(name, out hash);

        public bool TryGetMediaUrl(string url, out URLAddress result)
        {
            if (string.IsNullOrEmpty(url))
            {
                result = URLAddress.EMPTY;
                return false;
            }

            // Try resolve an internal URL
            if (TryGetContentUrl(url, out result))
                return true;

            if (!CHECK_ALLOWED_MEDIA_HOSTNAMES
                || (HasRequiredPermission(ScenePermissionNames.ALLOW_MEDIA_HOSTNAMES) && IsUrlDomainAllowed(url)))
            {
                result = URLAddress.FromString(url);
                return true;
            }

            result = URLAddress.EMPTY;
            return false;
        }

        public bool IsUrlDomainAllowed(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                foreach (string allowedMediaHostname in SceneEntityDefinition.metadata.allowedMediaHostnames)
                {
                    if (string.Equals(allowedMediaHostname, uri.Host, StringComparison.CurrentCultureIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        public bool IsSdk7() =>
            SceneEntityDefinition.metadata.runtimeVersion == "7";
    }
}
