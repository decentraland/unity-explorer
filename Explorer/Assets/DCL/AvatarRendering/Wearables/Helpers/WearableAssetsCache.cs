using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Pool;

namespace DCL.AvatarRendering.Wearables.Helpers
{
    /// <summary>
    ///     <para>
    ///         This class is used to store instances of wearable assets
    ///     </para>
    ///     <para>
    ///         It keeps a limited reasonable number of unique assets
    ///     </para>
    /// </summary>
    public class WearableAssetsCache : IWearableAssetsCache, IDisposable
    {
        // string is hash here which is retrieved via IWearable.GetMainFileHash
        private readonly Dictionary<GameObject, List<GameObject>> cache;
        private readonly ListObjectPool<GameObject> listPool;

        private readonly int maxNumberOfAssetsPerKey;
        private readonly Transform parentContainer;

        public WearableAssetsCache(int maxNumberOfAssetsPerKey, int initialCapacity)
        {
            this.maxNumberOfAssetsPerKey = maxNumberOfAssetsPerKey;

            var parentContainerGo = new GameObject($"POOL_CONTAINER_{nameof(WearableAssetsCache)}");
            parentContainerGo.SetActive(false);
            parentContainer = parentContainerGo.transform;

            cache = new Dictionary<GameObject, List<GameObject>>(initialCapacity);

            // instantiate a couple of lists to prevent runtime allocations
            listPool = new ListObjectPool<GameObject>(listInstanceDefaultCapacity: maxNumberOfAssetsPerKey, defaultCapacity: initialCapacity);
        }

        public bool TryGet(GameObject asset, out GameObject instance)
        {
            if (cache.TryGetValue(asset, out List<GameObject> list) && list.Count > 0)
            {
                // Remove from the tail of the list
                instance = list[^1];
                list.RemoveAt(list.Count - 1);
                return true;
            }

            instance = default(GameObject);
            return false;
        }

        public IWearableAssetsCache.ReleaseResult TryRelease(CachedWearable cachedWearable)
        {
            GameObject asset = cachedWearable.OriginalAsset.GameObject;
            GameObject instance = cachedWearable.Instance.GameObject;

            if (!cache.TryGetValue(asset, out List<GameObject> list))
                cache[asset] = list = listPool.Get();

            if (list.Count >= maxNumberOfAssetsPerKey)
                return IWearableAssetsCache.ReleaseResult.CapacityExceeded;

            list.Add(instance);

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return IWearableAssetsCache.ReleaseResult.EnvironmentIsDisposing;

            instance.SetActive(false);
            instance.transform.SetParent(parentContainer);
            return IWearableAssetsCache.ReleaseResult.ReturnedToPool;
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroyGameObject(parentContainer);
        }
    }
}
