using DCL.Platforms;
using System;

namespace DCL.Ipfs
{
public class AssetBundleManifestVersion
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;

        internal bool? HasHashInPathValue;

        public bool assetBundleManifestRequestFailed;
        public bool IsLSDAsset;
        public AssetBundleManifestVersionPerPlatform assets;

        private AssetBundleManifestVersion() { }

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
            {
                if (string.IsNullOrEmpty(GetAssetBundleManifestVersion()))
                    HasHashInPathValue = false;
                else
                    HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
            }

            return HasHashInPathValue.Value;
        }

        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.version : assets.mac.version;

        public string GetAssetBundleManifestBuildDate() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.buildDate : assets.mac.buildDate;

        public bool IsEmpty() =>
            assets.IsEmpty();

        public static AssetBundleManifestVersion CreateFailed()
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion
            {
                assetBundleManifestRequestFailed = true,
            };

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateLSDAsset()
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion
            {
                IsLSDAsset = true,
            };

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateManualManifest(string assetBundleManifestVersionMac, string assetBundleManifestVersionWin, string buildDate)
        {
            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.mac = new PlatformInfo(assetBundleManifestVersionMac, buildDate);
            ;
            assets.windows = new PlatformInfo(assetBundleManifestVersionWin, buildDate);
            ;

            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateFromFallback(string version, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(version, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPath();

            return assetBundleManifestVersion;
        }

        public static AssetBundleManifestVersion CreateForLOD(string assetBundleManifestVerison, string buildDate)
        {
            var assets = new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison, buildDate);

            var assetBundleManifestVersion = new AssetBundleManifestVersion();
            assetBundleManifestVersion.assets = assets;
            assetBundleManifestVersion.HasHashInPathValue = false;

            return assetBundleManifestVersion;
        }
    }

    public class AssetBundleManifestVersionPerPlatform
    {
        public PlatformInfo? mac;
        public PlatformInfo? windows;

        public void SetVersion(string assetBundleManifestVersion, string buildDate)
        {
            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                windows = new PlatformInfo(assetBundleManifestVersion, buildDate);
            else
                mac = new PlatformInfo(assetBundleManifestVersion, buildDate);
        }

        public bool IsEmpty() =>
            mac == null &&  windows == null;
    }

    public class PlatformInfo
    {
        public string version;
        public string buildDate;

        public PlatformInfo(string version, string buildDate)
        {
            this.version = version;
            this.buildDate = buildDate;
        }
    }
}
