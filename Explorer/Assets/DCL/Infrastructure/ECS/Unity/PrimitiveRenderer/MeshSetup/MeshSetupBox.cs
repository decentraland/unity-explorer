using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public class MeshSetupBox : IMeshSetup<BoxPrimitive>
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            BoxFactory.UpdateMesh(ref mesh, pbRenderer.Box.Uvs);
        }
    }
}
