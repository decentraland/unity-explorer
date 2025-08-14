using DCL.Platforms;
using System;

namespace DCL.Ipfs
{
    public enum AssetBundleRegistryEnum
    {
        complete,
        fallback,
        pending
    }

    public class AssetBundleManifestVersion
    {
        //From v25 onwards, the asset bundle path contains the sceneID in the hash
        //This was done to solve cache issues
        private const int ASSET_BUNDLE_VERSION_REQUIRES_HASH = 25;

        private bool? HasHashInPathValue;

        public AssetBundleManifestVersionPerPlatform assets;
        public bool assetBundleManifestRequestFailed;

        public AssetBundleManifestVersion() { }

        public AssetBundleManifestVersion(string assetBundleManifestVerison, string buildDate)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison, buildDate);
            HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
        }

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
                HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;

            return HasHashInPathValue.Value;
        }

        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.version : assets.mac.version;

        public string GetAssetBundleManifestBuildDate() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows.buildDate : assets.mac.buildDate;

        //Used only for manual creation
        public AssetBundleManifestVersion(string assetBundleManifestVersionMac, string assetBundleManifestVersionWin, string buildDate)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.mac = new PlatformInfo(assetBundleManifestVersionMac, buildDate);;
            assets.windows = new PlatformInfo(assetBundleManifestVersionWin, buildDate);;
            HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
        }

        public bool IsEmpty() =>
            assets.IsEmpty();
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
