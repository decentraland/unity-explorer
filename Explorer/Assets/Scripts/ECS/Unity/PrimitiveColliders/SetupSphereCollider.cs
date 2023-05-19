using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupSphereCollider : ISetupCollider<SphereCollider>
    {
        /// <summary>
        ///     TODO must be shared with the MeshRenderer Instantiator
        /// </summary>
        public const int SPHERE_RADIUS = 1;

        public void Execute(SphereCollider sphereCollider, PBMeshCollider meshCollider)
        {
            sphereCollider.radius = SPHERE_RADIUS;
        }
    }
}
