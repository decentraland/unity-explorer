using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public class MeshSetupSphere : IMeshSetup<SpherePrimitive>
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh) { }
    }
}
