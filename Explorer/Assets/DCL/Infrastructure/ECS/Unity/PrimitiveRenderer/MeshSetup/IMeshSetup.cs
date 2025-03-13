using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public interface IMeshSetup<T> : ISetupMesh where T: IPrimitiveMesh
    {
        Type ISetupMesh.MeshType => typeof(T);
    }

    public interface ISetupMesh
    {
        Type MeshType { get; }

        void Execute(PBMeshRenderer pbRenderer, Mesh mesh);
    }
}
