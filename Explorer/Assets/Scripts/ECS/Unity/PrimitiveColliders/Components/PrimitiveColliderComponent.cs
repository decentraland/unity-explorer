using System;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Components
{
    /// <summary>
    ///     A wrapper over the primitive collider that is created from the PBMeshCollider component.
    /// </summary>
    public struct PrimitiveColliderComponent : IPoolableComponentProvider<Collider>
    {
        public Collider Collider;
        public Type ColliderType;
        public PBMeshCollider.MeshOneofCase SDKType;

        Collider IPoolableComponentProvider<Collider>.PoolableComponent => Collider;
    }
}
