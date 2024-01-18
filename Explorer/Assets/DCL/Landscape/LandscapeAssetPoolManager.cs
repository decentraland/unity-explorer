using DCL.Optimization.Pools;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Landscape
{
    public class LandscapeAssetPoolManager
    {
        private readonly Dictionary<Transform, GameObjectPool<Transform>> pools = new ();
        private readonly Dictionary<Transform, int> counts = new ();

        public void Add(Transform asset, int prewarmCount)
        {
            var newPool = new FastGameObjectPool(null, () => Object.Instantiate(asset), collectionCheck: false);
            newPool.Prewarm(prewarmCount);
            pools[asset] = newPool;
            counts[asset] = 0;
        }

        public Transform Get(Transform prefab)
        {
            counts[prefab] += 1;
            return pools[prefab].Get();
        }

        public void Release(Transform prefab, Transform asset)
        {
            pools[prefab].Release(asset);
        }

        public void Print()
        {
            foreach (KeyValuePair<Transform, int> gameObjectPool in counts) { Debug.Log($"Object {gameObjectPool.Key.name} was spawned {gameObjectPool.Value} times"); }
        }
    }
}
