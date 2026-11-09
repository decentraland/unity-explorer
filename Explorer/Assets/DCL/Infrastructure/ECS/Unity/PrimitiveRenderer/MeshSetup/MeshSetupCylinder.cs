using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public class MeshSetupCylinder : IMeshSetup<CylinderPrimitive>
    {
        public void Execute(PBMeshRenderer pbRenderer, CylinderPrimitive primitiveMesh)
        {
            Mesh mesh = primitiveMesh.Mesh;
            CylinderVariantsFactory.Create(ref mesh, pbRenderer.Cylinder.GetTopRadius(), pbRenderer.Cylinder.GetBottomRadius());
        }
    }
}
