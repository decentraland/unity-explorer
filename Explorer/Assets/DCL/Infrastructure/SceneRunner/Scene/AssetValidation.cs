using DCL.Diagnostics;
using DCL.Ipfs;
using System;
using UnityEngine;

namespace SceneRunner.Scene
{
    public static class AssetValidation
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;
        public const string SceneIDError = "SceneID: ";
        public const string WearableIDError = "WearableID: ";

        public static bool ValidateSceneAssetBundleManifest(SceneAssetBundleManifest sceneAssetBundleManifest, string errorIDType, string errorID)
        {
            return ValidateVersion(sceneAssetBundleManifest.GetVersion(), errorIDType, errorID);
        }

        public static bool ValidateSceneAbDto(SceneAbDto sceneAbDto, string errorIDType, string errorID)
        {
            return ValidateVersion(sceneAbDto.version, errorIDType, errorID);
        }

        private static bool ValidateVersion(string version, string errorIDType, string errorID)
        {
            if (string.IsNullOrEmpty(version))
                return true;

            var intVersion = int.Parse(version.AsSpan().Slice(1));
            int supportedVersion;

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    supportedVersion = AB_MIN_SUPPORTED_VERSION_WINDOWS;
                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    supportedVersion = AB_MIN_SUPPORTED_VERSION_MAC;
                    break;
                default:
                    return true;
            }

            if (intVersion < supportedVersion)
            {
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}, Asset bundle {errorIDType + errorID} requires rebuild");
                return false;
            }

            return true;
        }
    }
}
