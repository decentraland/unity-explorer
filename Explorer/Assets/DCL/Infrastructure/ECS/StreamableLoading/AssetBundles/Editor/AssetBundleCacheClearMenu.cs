using UnityEditor;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Editor
{
    public static class AssetBundleCacheClearMenu
    {
        [MenuItem("Decentraland/Cache/Clear AssetBundle cache")]
        public static void ClearAssetBundles()
        {
            if (!Caching.ClearCache())
                EditorUtility.DisplayDialog("WARNING", "Failed to clear cache, try resetting Unity and run the action again", "Ok");
        }
    }
}
