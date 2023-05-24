using DCL.ECSComponents;
using ECS.ComponentsPooling;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Components
{
    /// <summary>
    ///     A wrapper over the primitive collider that is created from the PBMeshCollider component.
    /// </summary>
    public struct PrimitiveColliderComponent : IPoolableComponentProvider
    {
        public Collider Collider;
        public Type ColliderType;
        public PBMeshCollider.MeshOneofCase SDKType;

        object IPoolableComponentProvider.PoolableComponent => Collider;
        Type IPoolableComponentProvider.PoolableComponentType => ColliderType;

        public void Dispose() { }
    }
}
