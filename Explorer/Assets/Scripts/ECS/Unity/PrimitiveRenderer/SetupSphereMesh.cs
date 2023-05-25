using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupSphereMesh : ISetupMesh
    {
        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            SphereFactory.Create(ref mesh);
        }
    }
}
