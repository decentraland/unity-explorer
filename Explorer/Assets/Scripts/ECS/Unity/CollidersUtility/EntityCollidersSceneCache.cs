using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    public class EntityCollidersSceneCache : IEntityCollidersSceneCache
    {
        private static readonly ThreadSafeObjectPool<EntityCollidersSceneCache> POOL = new (
            () => new EntityCollidersSceneCache(), defaultCapacity: PoolConstants.SCENES_COUNT);

        private readonly Dictionary<Collider, ColliderSceneEntityInfo> map = new (100);

        private IEntityCollidersGlobalCache globalCache;

        private EntityCollidersSceneCache() { }

        public void Dispose()
        {
            globalCache.RemoveSceneInfo(this);
            map.Clear();
            POOL.Release(this);
        }

        public bool TryGetEntity(Collider collider, out ColliderSceneEntityInfo sceneEntity) =>
            map.TryGetValue(collider, out sceneEntity);

        public void Associate(Collider collider, ColliderSceneEntityInfo sceneEntityInfo)
        {
            map[collider] = sceneEntityInfo;
            globalCache.Associate(collider, this, sceneEntityInfo);
        }

        public void Remove(Collider collider)
        {
            if (collider)
            {
                map.Remove(collider);
                globalCache.RemoveAssociation(collider);
            }
        }

        public static EntityCollidersSceneCache Create(IEntityCollidersGlobalCache globalCache)
        {
            EntityCollidersSceneCache cache = POOL.Get();
            cache.globalCache = globalCache;
            return cache;
        }
    }
}
