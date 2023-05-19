using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupPlaneCollider : ISetupCollider<BoxCollider>
    {
        internal static readonly Vector3 SIZE = new (1, 1, 0.01f);

        public void Execute(BoxCollider boxCollider, PBMeshCollider meshCollider)
        {
            boxCollider.size = SIZE;
        }
    }
}
