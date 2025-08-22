using DCL.Diagnostics;
using DCL.Platforms;
using System;

namespace SceneRunner.Scene
{
    public static class AssetValidation
    {
        public const int AB_MIN_SUPPORTED_VERSION_WINDOWS = 15;
        public const int AB_MIN_SUPPORTED_VERSION_MAC = 16;

        public static void ValidateSceneAbDto(string version, string hash)
        {
            if (string.IsNullOrEmpty(version))
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version missing for {hash}");

            var intVersion = int.Parse(version.AsSpan().Slice(1));
            int supportedVersion  = IPlatform.DEFAULT.CurrentPlatform() == IPlatform.Kind.Windows ? AB_MIN_SUPPORTED_VERSION_WINDOWS : AB_MIN_SUPPORTED_VERSION_MAC;

            if (intVersion < supportedVersion)
                ReportHub.LogError(ReportCategory.ASSET_BUNDLES, $"Asset bundle version {intVersion} is not supported. Minimum supported version is {supportedVersion}, Asset bundle {hash} requires rebuild");
        }

    }
}
