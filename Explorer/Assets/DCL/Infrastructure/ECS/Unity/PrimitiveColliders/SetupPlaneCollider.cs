using DCL.ECSComponents;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupPlaneCollider : ISetupCollider<BoxCollider>
    {
        public void Execute(BoxCollider boxCollider, PBMeshCollider meshCollider)
        {
            boxCollider.size = PrimitivesSize.PLANE_SIZE;
        }
    }
}
