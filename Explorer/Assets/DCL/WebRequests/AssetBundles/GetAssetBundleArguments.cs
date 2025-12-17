using Newtonsoft.Json;
using UnityEngine;

namespace DCL.WebRequests
{
    public readonly struct GetAssetBundleArguments
    {
        [JsonIgnore]
        public readonly AssetBundleLoadingMutex LoadingMutex;
        public readonly Hash128? CacheHash;
        public readonly bool AutoLoadAssetBundle;

        public GetAssetBundleArguments(AssetBundleLoadingMutex loadingMutex, Hash128? cacheHash, bool autoLoadAssetBundle = false)
        {
            CacheHash = cacheHash;
            AutoLoadAssetBundle = autoLoadAssetBundle;
            LoadingMutex = loadingMutex;
        }

        [JsonConstructor]
        private GetAssetBundleArguments(Hash128? cacheHash, bool autoLoadAssetBundle = false)
        {
            LoadingMutex = null;
            CacheHash = cacheHash;
            AutoLoadAssetBundle = autoLoadAssetBundle;
        }
    }
}
