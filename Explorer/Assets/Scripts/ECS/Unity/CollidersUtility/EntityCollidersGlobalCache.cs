using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    public class EntityCollidersGlobalCache : IEntityCollidersGlobalCache
    {
        private readonly Dictionary<IEntityCollidersSceneCache, SceneEcsExecutor> scenesInfo = new (PoolConstants.SCENES_COUNT);
        private readonly Dictionary<Collider, GlobalColliderEntityInfo> colliderEntityInfos = new (100 * PoolConstants.SCENES_COUNT);

        public bool TryGetEntity(Collider collider, out GlobalColliderEntityInfo entity) =>
            colliderEntityInfos.TryGetValue(collider, out entity);

        public void AddSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache, SceneEcsExecutor sceneEcsExecutor)
        {
            scenesInfo.Add(entityCollidersSceneCache, sceneEcsExecutor);
        }

        public void RemoveSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache)
        {
            scenesInfo.Remove(entityCollidersSceneCache);
        }

        public void Associate(Collider collider, IEntityCollidersSceneCache sceneCache, ColliderEntityInfo colliderEntityInfo)
        {
            if (scenesInfo.TryGetValue(sceneCache, out SceneEcsExecutor ecsExecutor))
                colliderEntityInfos[collider] = new GlobalColliderEntityInfo(ecsExecutor, colliderEntityInfo);
        }

        public void RemoveAssociation(Collider collider)
        {
            colliderEntityInfos.Remove(collider);
        }
    }
}
