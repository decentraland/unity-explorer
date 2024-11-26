using DCL.Diagnostics;
using System;
using UnityEngine;

namespace SceneRunner.Scene
{
    public class AssetValidation
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        public static bool ValidateSceneAssetBundleManifest(SceneAssetBundleManifest sceneAssetBundleManifest)
        {
            string errorText = "SceneID: " + sceneAssetBundleManifest.GetSceneID();
            return ValidateVersion(sceneAssetBundleManifest.GetVersion(), errorText);
        }

        public static bool ValidateSceneAbDto_Hash(SceneAbDto sceneAbDto, string hash)
        {
            string errorText = "Wearable Hash: " + hash;
            return ValidateVersion(sceneAbDto.version, errorText);
        }

        public static bool ValidateSceneAbDto_SceneID(SceneAbDto sceneAbDto, string sceneID)
        {
            string errorText = "Wearable SceneID: " + sceneID;
            return ValidateVersion(sceneAbDto.version, errorText);
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
