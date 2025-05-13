using DCL.ECSComponents;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupSphereCollider : ISetupCollider<SphereCollider>
    {
        public void Execute(SphereCollider sphereCollider, PBMeshCollider meshCollider)
        {
            sphereCollider.radius = PrimitivesSize.SPHERE_RADIUS;
        }
    }
}
