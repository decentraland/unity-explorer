using DCL.Diagnostics;
using System;
using UnityEngine;

namespace SceneRunner.Scene
{
    static public class AssetValidation
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        public static bool ValidateSceneAssetBundleManifest(SceneAssetBundleManifest sceneAssetBundleManifest, string errorMessage)
        {
            return ValidateVersion(sceneAssetBundleManifest.GetVersion(), errorMessage);
        }

        public static bool ValidateSceneAbDto(SceneAbDto sceneAbDto, string errorMessage)
        {
            return ValidateVersion(sceneAbDto.version, errorMessage);
        }

        private static bool ValidateVersion(string version, string errorText)
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
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}, Asset bundle {errorText} requires rebuild");
                return false;
            }

            return true;
        }
    }
}
