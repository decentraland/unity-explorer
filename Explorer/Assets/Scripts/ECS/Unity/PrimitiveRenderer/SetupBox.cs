using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupBox : ISetupMesh
    {
        //private readonly Mesh reusableCubeMesh;

        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            CubeFactory.Create(ref mesh);

            if (pbRenderer.Box.Uvs is { Count: > 0 })
                mesh.uv = PrimitivesUtility.FloatArrayToV2List(pbRenderer.Box.Uvs);
        }
    }
}
