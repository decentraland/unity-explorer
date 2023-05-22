using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupBoxCollider : ISetupCollider<BoxCollider>
    {
        /// <summary>
        ///     TODO must be shared with the MeshRenderer instantiator
        /// </summary>
        public const int CUBE_SIZE = 1;

        public void Execute(BoxCollider collider, PBMeshCollider meshCollider)
        {
            collider.size = new Vector3(CUBE_SIZE, CUBE_SIZE, CUBE_SIZE);
        }
    }
}
