using System.Collections.Generic;

namespace ECS.StreamableLoading
{
    public struct AssetBundlesVersions
    {
        public readonly Dictionary<string, PlatformVersionInfo> versions;

        public static AssetBundlesVersions Create()
        {
            return new AssetBundlesVersions (true);
        }

        private AssetBundlesVersions(bool paramLess)
        {
            versions = new Dictionary<string, PlatformVersionInfo>();
        }

        public struct PlatformVersionInfo
        {
            public VersionInfo mac;
            public VersionInfo windows;
        }

        public struct VersionInfo
        {
            public string version;
            public string buildDate;
        }
    }
}