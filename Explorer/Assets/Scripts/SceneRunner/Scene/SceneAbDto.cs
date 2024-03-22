using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneRunner.Scene
{
    // this datatype is defined by https://github.com/decentraland/asset-bundle-converter
    [Serializable]
    public struct SceneAbDto
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        [SerializeField]
        internal bool ignoreValidation;
        [SerializeField]
        internal string version;
        [SerializeField]
        internal string[] files;
        [SerializeField]
        private int exitCode;

        public string Version => version;
        public IReadOnlyList<string> Files => files ?? Array.Empty<string>();

        public bool ValidateVersion()
        {
            if (ignoreValidation)
                return true;
            
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
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}");
                return false;
            }

            return true;
        }
    }
}
