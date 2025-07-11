using Best.HTTP.Caching;
using Best.HTTP.Shared;
using UnityEditor;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Editor
{
    public static class AssetBundleCacheClearMenu
    {
        [MenuItem("Decentraland/Clear AssetBundle cache")]
        public static void ClearAssetBundles()
        {
            if (!Caching.ClearCache())
                EditorUtility.DisplayDialog("WARNING", "Failed to clear cache, try resetting Unity and run the action again", "Ok");

            // Clear HTTP2 Cache
            // It does not matter to which values we assign it - we will clear it entirely anyway
            using var cache = new HTTPCache(new HTTPCacheOptions());
            HTTPManager.LocalCache = cache;
            cache.Clear();
            HTTPManager.LocalCache = null;
        }
    }
}
