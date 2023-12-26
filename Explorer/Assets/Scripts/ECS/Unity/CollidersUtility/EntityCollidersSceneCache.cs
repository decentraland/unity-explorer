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

        private readonly Dictionary<Collider, ColliderEntityInfo> map = new (100);

        private IEntityCollidersGlobalCache globalCache;

        private EntityCollidersSceneCache() { }

        public void Dispose()
        {
            globalCache.RemoveSceneInfo(this);
            map.Clear();
            POOL.Release(this);
        }

        public bool TryGetEntity(Collider collider, out ColliderEntityInfo entity) =>
            map.TryGetValue(collider, out entity);

        public void Associate(Collider collider, ColliderEntityInfo entityInfo)
        {
            map[collider] = entityInfo;
            globalCache.Associate(collider, this, entityInfo);
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
