using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Unity.SceneBoundsChecker;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Components
{
    /// <summary>
    ///     A wrapper over the primitive collider that is created from the PBMeshCollider component.
    /// </summary>
    public struct PrimitiveColliderComponent : IPoolableComponentProvider<Collider>
    {
        public readonly Collider? Collider => SDKCollider.Collider;

        public SDKCollider SDKCollider;

        public Type ColliderType { get; private set; }
        public PBMeshCollider.MeshOneofCase SDKType { get; private set; }

        public static PrimitiveColliderComponent NewInvalidCollider() =>
            new () { ColliderType = typeof(SphereCollider), SDKType = PBMeshCollider.MeshOneofCase.None };

        public PrimitiveColliderComponent(SDKCollider sdkCollider, Type colliderType, PBMeshCollider.MeshOneofCase sdkType)
        {
            SDKCollider = sdkCollider;
            ColliderType = colliderType;
            SDKType = sdkType;
        }

        public void AssignCollider(SDKCollider collider, Type colliderType, PBMeshCollider.MeshOneofCase sdkType)
        {
            SDKCollider = collider;
            ColliderType = colliderType;
            SDKType = sdkType;
        }

        public void Invalidate()
        {
            SDKCollider = SDKCollider.NewInvalidSDKCollider();
        }

        Collider IPoolableComponentProvider<Collider>.PoolableComponent => Collider!;

        Type IPoolableComponentProvider<Collider>.PoolableComponentType => ColliderType;

        public void Dispose() { }
    }
}
