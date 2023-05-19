using DCL.ECSComponents;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Components
{
    /// <summary>
    ///     A wrapper over the primitive collider that is created from the PBMeshCollider component.
    /// </summary>
    public struct PrimitiveColliderComponent
    {
        public Collider Collider;
        public Type ColliderType;
        public PBMeshCollider.MeshOneofCase SDKType;
    }
}
