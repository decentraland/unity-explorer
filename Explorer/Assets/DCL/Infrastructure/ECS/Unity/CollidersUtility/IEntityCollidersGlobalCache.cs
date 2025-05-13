using Arch.Core;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    /// <summary>
    ///     Association between Colliders and Entities, to be accessed from the global scene
    /// </summary>
    public interface IEntityCollidersGlobalCache
    {
        bool TryGetSceneEntity(Collider collider, out GlobalColliderSceneEntityInfo sceneEntity);

        bool TryGetGlobalEntity(Collider collider, out GlobalColliderGlobalEntityInfo entity);

        void AddSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache, SceneEcsExecutor sceneEcsExecutor);

        void RemoveSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache);

        /// <summary>
        ///     Associate a collider with a SCENE cache
        ///     where the information about the entity itself is stored
        /// </summary>
        void Associate(Collider collider, IEntityCollidersSceneCache sceneCache, ColliderSceneEntityInfo colliderSceneEntityInfo);

        /// <summary>
        ///     Associate a collider with the GLOBAL WORLD cache
        ///     where the information about the entity itself is stored
        /// </summary>
        void Associate(Collider collider, EntityReference entityReference);

        /// <summary>
        ///     Remove association with the collider (for scene entities)
        /// </summary>
        void RemoveSceneEntityAssociation(Collider collider);

        /// <summary>
        ///     Remove association with the collider (for global entities)
        /// </summary>
        void RemoveGlobalEntityAssociation(Collider collider);
    }
}
