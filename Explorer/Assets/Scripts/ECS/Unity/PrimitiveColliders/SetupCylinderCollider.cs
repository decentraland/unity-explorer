using CrdtEcsBridge.Components.Defaults;
using DCL.ECSComponents;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveColliders
{
    internal class SetupCylinderCollider : ISetupCollider<MeshCollider>
    {
        public void Execute(MeshCollider collider, PBMeshCollider meshCollider)
        {
            Mesh mesh = collider.sharedMesh;
            CylinderVariantsFactory.Create(ref mesh, meshCollider.Cylinder.GetTopRadius(), meshCollider.Cylinder.GetBottomRadius());
            collider.sharedMesh = mesh;
        }
    }
}
