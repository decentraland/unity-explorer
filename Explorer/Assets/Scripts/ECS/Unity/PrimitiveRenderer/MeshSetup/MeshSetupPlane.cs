using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public class MeshSetupPlane : IMeshSetup<PlanePrimitive>
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            PlaneFactory.UpdateMesh(ref mesh, pbRenderer.Plane.Uvs);
        }
    }
}
