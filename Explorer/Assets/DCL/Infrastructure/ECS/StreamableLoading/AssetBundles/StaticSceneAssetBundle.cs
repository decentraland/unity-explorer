using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct StaticSceneAssetBundle
    {
        public AssetBundleData assetBundleData;
        public Dictionary<string, GameObject> assets;

    }
}
