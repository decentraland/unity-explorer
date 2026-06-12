using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading
{
    public struct AssetBundlesVersions
    {
        public Dictionary<string, PlatformVersionInfo> versions { get; private set; }

        public static AssetBundlesVersions Create() =>
            new ()
            {
                versions = new Dictionary<string, PlatformVersionInfo>(StringComparer.InvariantCultureIgnoreCase)
            };

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
