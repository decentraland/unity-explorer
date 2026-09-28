using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public class MeshSetupBox : IMeshSetup<BoxPrimitive>
    {
        public void Execute(PBMeshRenderer pbRenderer, BoxPrimitive primitiveMesh)
        {
            primitiveMesh.ApplyUVs(pbRenderer.Box.Uvs);
        }
    }
}
