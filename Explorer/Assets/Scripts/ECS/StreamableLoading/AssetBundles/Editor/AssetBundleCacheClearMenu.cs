using UnityEditor;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Editor
{
    public static class AssetBundleCacheClearMenu
    {
        [MenuItem("Decentraland/Clear AssetBundle cache")]
        public static void ClearAssetBundles()
        {
            Caching.ClearCache();
        }
    }
}
