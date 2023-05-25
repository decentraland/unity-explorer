using DCL.ECSComponents;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupBoxMesh : ISetupMesh
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            BoxFactory.Create(ref mesh);

            if (pbRenderer.Box.Uvs is { Count: > 0 })
                mesh.uv = PrimitivesUtility.FloatArrayToV2List(pbRenderer.Box.Uvs);
        }
    }
}
