using DCL.ECSComponents;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders
{
    public interface ISetupCollider<in T> : ISetupCollider where T: Collider
    {
        Type ISetupCollider.ColliderType => typeof(T);

        void ISetupCollider.Execute(Collider collider, PBMeshCollider meshCollider) =>
            Execute((T)collider, meshCollider);

        void Execute(T collider, PBMeshCollider meshCollider);
    }

    public interface ISetupCollider
    {
        Type ColliderType { get; }

        void Execute(Collider collider, PBMeshCollider meshCollider);
    }
}
