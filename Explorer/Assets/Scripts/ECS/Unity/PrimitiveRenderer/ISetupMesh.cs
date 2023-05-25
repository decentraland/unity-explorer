using DCL.ECSComponents;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer
{
    public interface ISetupMesh
    {
        void Execute(PBMeshRenderer pbRenderer, Mesh mesh);
    }
}
