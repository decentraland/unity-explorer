using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer
{
    public class SetupSphere : ISetupMesh
    {
        //private readonly Mesh reusableSphere;

        public void Execute(PBMeshRenderer pbRenderer, Mesh mesh)
        {
            SphereFactory.Create(ref mesh);
        }
    }
}
