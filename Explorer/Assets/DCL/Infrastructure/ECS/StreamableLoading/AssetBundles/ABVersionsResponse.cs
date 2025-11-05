using Newtonsoft.Json;
using System;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public struct ABVersionsResponse : ISerializationCallbackReceiver
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
            public ABAssetVersionInfo webgl;
            public ABAssetVersionInfo windows;
        }

        [Serializable]
        public struct ABAssetVersionInfo
        {
            public string version;
            public string buildDate;
            public DateTime ProcessedBuildDate;
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
            public string webgl;
            public string windows;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (DateTime.TryParse(versions.assets.mac.buildDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateMac))
                versions.assets.mac.ProcessedBuildDate = dateMac;
            if (DateTime.TryParse(versions.assets.webgl.buildDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateWebgl))
                versions.assets.webgl.ProcessedBuildDate = dateWebgl;
            if (DateTime.TryParse(versions.assets.windows.buildDate, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateWin))
                versions.assets.windows.ProcessedBuildDate = dateWin;
        }
    }
}
