using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Landscape
{
    public class LandscapeAssetPoolManager
    {
        private readonly Dictionary<Transform, GameObjectPool<Transform>> pools = new ();

        public void Add(Transform asset, int prewarmCount)
        {
            var newPool = new FastGameObjectPool(null, () => Object.Instantiate(asset), collectionCheck: false);
            newPool.Prewarm(prewarmCount);
            pools[asset] = newPool;
        }

        public Transform Get(Transform prefab) =>
            pools[prefab].Get();

        public void Release(Transform prefab, Transform asset)
        {
            pools[prefab].Release(asset);
        }
    }
}
