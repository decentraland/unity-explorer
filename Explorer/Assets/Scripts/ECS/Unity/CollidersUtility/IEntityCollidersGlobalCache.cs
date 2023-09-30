using SceneRunner.Scene;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    /// <summary>
    ///     Association between Colliders and Entities, to be accessed from the global scene
    /// </summary>
    public interface IEntityCollidersGlobalCache
    {
        bool TryGetEntity(Collider collider, out GlobalColliderEntityInfo entity);

        void AddSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache, SceneEcsExecutor sceneEcsExecutor);

        void RemoveSceneInfo(IEntityCollidersSceneCache entityCollidersSceneCache);

        /// <summary>
        ///     Associate a collider with a scene cache
        ///     where the information about the entity itself is stored
        /// </summary>
        void Associate(Collider collider, IEntityCollidersSceneCache sceneCache, ColliderEntityInfo colliderEntityInfo);

        /// <summary>
        ///     Remove association with the collider
        /// </summary>
        void RemoveAssociation(Collider collider);
    }
}
