using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupBoxCollider : ISetupCollider<BoxCollider>
    {
        public void Execute(BoxCollider collider, PBMeshCollider meshCollider)
        {
            collider.size = Vector3.one * PrimitivesSize.CUBE_SIZE;
        }
    }
}
