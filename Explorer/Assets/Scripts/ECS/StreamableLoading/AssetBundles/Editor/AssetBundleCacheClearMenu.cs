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
                Debug.LogError("BEWARE, THE CACHE HAS NOT BEEN DELETED. TRY RESETTING UNITY AND CALLING IT AGAIN");
        }
    }
}
