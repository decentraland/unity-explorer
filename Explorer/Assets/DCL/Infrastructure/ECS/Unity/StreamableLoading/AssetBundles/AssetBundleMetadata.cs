using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.AssetBundles
{
    [Serializable]
    public class AssetBundleMetadata
    {
        public long timestamp = -1;
        public string version = "1.0";
        public List<string> dependencies;
        public string mainAsset;

        public void Clear()
        {
            timestamp = -1;
            version = "1.0";
            dependencies.Clear();
            mainAsset = "";
        }
    }
}
