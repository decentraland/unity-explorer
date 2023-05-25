using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupPlaneMesh : ISetupMesh
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            PlaneFactory.Create(ref mesh);

            if (pbRenderer.Plane.Uvs is { Count: > 0 })
                mesh.uv = PrimitivesUtility.FloatArrayToV2List(pbRenderer.Plane.Uvs);
        }
    }
}
