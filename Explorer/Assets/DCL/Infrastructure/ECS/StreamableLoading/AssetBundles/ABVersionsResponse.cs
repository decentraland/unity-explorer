using System;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public struct ABVersionsResponse
    {
        public string[] pointers;
        public ABVersion versions;
        public ABBundles bundles;
        public string status;

        [Serializable]
        public struct ABVersion
        {
            public ABAssets assets;
        }

        [Serializable]
        public struct ABAssets
        {
            public ABAssetVersionInfo mac;
            public ABAssetVersionInfo windows;
        }

        [Serializable]
        public struct ABAssetVersionInfo
        {
            public string version;
            public string buildDate;
        }

        [Serializable]
        public struct ABBundles
        {
            public ABBundleStatus lods;
            public ABBundleStatus assets;
        }

        [Serializable]
        public struct ABBundleStatus
        {
            public string mac;
            public string windows;
        }
    }
}
