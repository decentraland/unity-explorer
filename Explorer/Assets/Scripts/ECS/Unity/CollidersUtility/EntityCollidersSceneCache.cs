using System.Collections.Generic;
using UnityEngine;
using Utility.Pool;
using Utility.ThreadSafePool;

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
            POOL.Release(this);
        }

        public bool TryGetEntity(Collider collider, out ColliderEntityInfo entity) =>
            map.TryGetValue(collider, out entity);

        public void Associate(Collider collider, ColliderEntityInfo entityInfo)
        {
            map[collider] = entityInfo;
            globalCache.Associate(collider, this, entityInfo);
        }

        public void Associate(IEnumerable<Collider> colliders, ColliderEntityInfo entityInfo)
        {
            foreach (Collider collider in colliders)
            {
                map[collider] = entityInfo;
                globalCache.Associate(collider, this, entityInfo);
            }
        }

        public void Remove(Collider collider)
        {
            if (collider)
            {
                map.Remove(collider);
                globalCache.RemoveAssociation(collider);
            }
        }

        public void Remove(IEnumerable<Collider> colliders)
        {
            foreach (Collider collider in colliders)
                Remove(collider);
        }

        public static EntityCollidersSceneCache Create(IEntityCollidersGlobalCache globalCache)
        {
            EntityCollidersSceneCache cache = POOL.Get();
            cache.globalCache = globalCache;
            return cache;
        }
    }
}
