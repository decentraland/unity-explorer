using UnityEngine;

namespace DCL.WebRequests
{
    public readonly struct GetAssetBundleArguments
    {
        public readonly AssetBundleLoadingMutex LoadingMutex;
        public readonly Hash128? CacheHash;
        public readonly bool AutoLoadAssetBundle;

        public GetAssetBundleArguments(AssetBundleLoadingMutex loadingMutex, Hash128? cacheHash, bool autoLoadAssetBundle = false)
        {
            CacheHash = cacheHash;
            AutoLoadAssetBundle = autoLoadAssetBundle;
            LoadingMutex = loadingMutex;
        }
    }
}
