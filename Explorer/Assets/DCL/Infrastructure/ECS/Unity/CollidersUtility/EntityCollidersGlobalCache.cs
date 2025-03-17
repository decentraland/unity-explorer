using Arch.Core;
using DCL.Optimization.Pools;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    public class EntityCollidersGlobalCache : IEntityCollidersGlobalCache
    {
        private readonly Dictionary<IEntityCollidersSceneCache, SceneEcsExecutor> scenesInfo = new (PoolConstants.SCENES_COUNT);
        private readonly Dictionary<Collider, GlobalColliderSceneEntityInfo> colliderSceneEntityInfos = new (100 * PoolConstants.SCENES_COUNT);
        private readonly Dictionary<Collider, GlobalColliderGlobalEntityInfo> colliderGlobalEntityInfos = new (100 * PoolConstants.GLOBAL_WORLD_COUNT);

        public bool TryGetSceneEntity(Collider collider, out GlobalColliderSceneEntityInfo sceneEntity) =>
            colliderSceneEntityInfos.TryGetValue(collider, out sceneEntity);

        public bool TryGetGlobalEntity(Collider collider, out GlobalColliderGlobalEntityInfo entity) =>
            colliderGlobalEntityInfos.TryGetValue(collider, out entity);

        public void AddSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache, SceneEcsExecutor sceneEcsExecutor)
        {
            scenesInfo.Add(entityCollidersSceneCache, sceneEcsExecutor);
        }

        public void RemoveSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache)
        {
            scenesInfo.Remove(entityCollidersSceneCache);
        }

        public void Associate(Collider collider, IEntityCollidersSceneCache sceneCache, ColliderSceneEntityInfo colliderSceneEntityInfo)
        {
            if (scenesInfo.TryGetValue(sceneCache, out SceneEcsExecutor ecsExecutor))
                colliderSceneEntityInfos[collider] = new GlobalColliderSceneEntityInfo(ecsExecutor, colliderSceneEntityInfo);
        }

        public void Associate(Collider collider, EntityReference entityReference)
        {
            colliderGlobalEntityInfos[collider] = new GlobalColliderGlobalEntityInfo(entityReference);
        }

        public void RemoveSceneEntityAssociation(Collider collider) =>
            colliderSceneEntityInfos.Remove(collider);

        public void RemoveGlobalEntityAssociation(Collider collider) =>
            colliderGlobalEntityInfos.Remove(collider);
    }
}
