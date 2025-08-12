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
        public string assetBundleBuildDate;

        public AssetBundleManifestVersion() { }

        public AssetBundleManifestVersion(string assetBundleManifestVerison)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison);
            HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
        }

        public bool HasHashInPath()
        {
            if (HasHashInPathValue == null)
                HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;

            return HasHashInPathValue.Value;
        }

        public string GetAssetBundleManifestVersion() =>
            IPlatform.DEFAULT.Is(IPlatform.Kind.Windows) ? assets.windows : assets.mac;

        //Used only for manual creation
        public AssetBundleManifestVersion(string assetBundleManifestVersionMac, string assetBundleManifestVersionWin)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.mac = assetBundleManifestVersionMac;
            assets.windows = assetBundleManifestVersionWin;
            HasHashInPathValue = int.Parse(GetAssetBundleManifestVersion().AsSpan().Slice(1)) >= ASSET_BUNDLE_VERSION_REQUIRES_HASH;
        }

        public bool IsEmpty() =>
            assets.IsEmpty();
    }

    public class AssetBundleManifestVersionPerPlatform
    {
        public string mac;
        public string windows;

        public void SetVersion(string assetBundleManifestVersion)
        {
            if (IPlatform.DEFAULT.Is(IPlatform.Kind.Windows))
                windows = assetBundleManifestVersion;
            else
                mac = assetBundleManifestVersion;
        }

        public bool IsEmpty() =>
            string.IsNullOrEmpty(mac) &&  string.IsNullOrEmpty(windows);
    }

}
