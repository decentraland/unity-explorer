using Arch.Core;
using DCL.SDKComponents.Tween.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
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
        void Associate(Collider collider, Entity entityReference);

        /// <summary>
        ///     Remove association with the collider (for scene entities)
        /// </summary>
        void RemoveSceneEntityAssociation(Collider collider);

        /// <summary>
        ///     Remove association with the collider (for global entities)
        /// </summary>
        void RemoveGlobalEntityAssociation(Collider collider);

        Dictionary<Collider, GlobalColliderSceneEntityInfo> colliderSceneEntityInfos { get; }
        Dictionary<(uint entityId, ulong networkId), (ITweener, Transform)> NetworkEntityToSceneEntity { get; }
    }
}
