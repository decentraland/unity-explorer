using ECS.ComponentsPooling;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Landscape
{
    public class LandscapeAssetPoolManager
    {
        private readonly Dictionary<Transform, GameObjectPool<Transform>> pools = new ();

        public void Add(Transform asset)
        {
            pools[asset] = new FastGameObjectPool(null, () => Object.Instantiate(asset), collectionCheck: false);
            ;
        }

        public Transform Get(Transform prefab) =>
            pools[prefab].Get();

        public void Release(Transform prefab, Transform asset)
        {
            pools[prefab].Release(asset);
        }
    }
}
