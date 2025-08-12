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
        public AssetBundleManifestVersionPerPlatform assets;

        public AssetBundleManifestVersion() { }

        public AssetBundleManifestVersion(string assetBundleManifestVerison)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.SetVersion(assetBundleManifestVerison);
        }

        //Used only for manual creation
        public AssetBundleManifestVersion(string assetBundleManifestVersionMac, string assetBundleManifestVersionWin)
        {
            assets =  new AssetBundleManifestVersionPerPlatform();
            assets.mac = assetBundleManifestVersionMac;
            assets.windows = assetBundleManifestVersionWin;
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
