using Diagnostics;
using Ipfs;
using JetBrains.Annotations;
using System;
using UnityEngine;
using Utility;

namespace SceneRunner.Scene
{
    public class SceneData : ISceneData
    {
        private readonly ISceneContent sceneContent;
        private readonly IpfsTypes.SceneEntityDefinition sceneDefinition;

        public SceneData(
            ISceneContent sceneContent,
            IpfsTypes.SceneEntityDefinition sceneDefinition,
            [NotNull] SceneAssetBundleManifest assetBundleManifest,
            Vector2Int baseParcel,
            StaticSceneMessages staticSceneMessages)
        {
            this.sceneContent = sceneContent;
            this.sceneDefinition = sceneDefinition;
            AssetBundleManifest = assetBundleManifest;
            StaticSceneMessages = staticSceneMessages;
            SceneShortInfo = new SceneShortInfo(baseParcel, sceneDefinition.id);
            BasePosition = ParcelMathHelper.GetPositionByParcelPosition(SceneShortInfo.BaseParcel);
        }

        public StaticSceneMessages StaticSceneMessages { get; }
        public SceneShortInfo SceneShortInfo { get; }
        public Vector3 BasePosition { get; }
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

        public bool TryGetContentUrl(string url, out string result) =>
            sceneContent.TryGetContentUrl(url, out result);

        public bool TryGetHash(string name, out string hash) =>
            sceneContent.TryGetHash(name, out hash);

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

        public bool IsSdk7() =>
            sceneDefinition.metadata.runtimeVersion == "7";
    }
}
