using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Interaction.Utility
{
    /// <summary>
    ///     Entity Colliders cache that is encapsulated in a scene,
    ///     used as a fast lookup for raycasts
    /// </summary>
    public interface IEntityCollidersSceneCache : IDisposable
    {
        /// <summary>
        ///     Get information attached to the specified collider
        /// </summary>
        /// <returns></returns>
        bool TryGetEntity(Collider collider, out ColliderSceneEntityInfo sceneEntity);

        /// <summary>
        ///     Cache Collider for alive entity, if collider is already cached the entity information will be overriden
        /// </summary>
        void Associate(Collider collider, ColliderSceneEntityInfo sceneEntityInfo);

        /// <summary>
        ///     Remove collider association
        /// </summary>
        void Remove([CanBeNull] Collider collider);
    }
}
