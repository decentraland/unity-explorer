using CrdtEcsBridge.Components.Defaults;
using DCL.ECSComponents;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupCylinder : ISetupMesh
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            CylinderVariantsFactory.Create(ref mesh, pbRenderer.Cylinder.GetTopRadius(), pbRenderer.Cylinder.GetBottomRadius());
        }
    }
}
