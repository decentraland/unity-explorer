using DCL.ECSComponents;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using System;

namespace ECS.Unity.PrimitiveRenderer.MeshSetup
{
    public interface IMeshSetup<in T> : ISetupMesh where T: IPrimitiveMesh
    {
        Type ISetupMesh.MeshType => typeof(T);

        void ISetupMesh.Execute(PBMeshRenderer pbRenderer, IPrimitiveMesh primitiveMesh) =>
            Execute(pbRenderer, (T)primitiveMesh);

        void Execute(PBMeshRenderer pbRenderer, T primitiveMesh);
    }

    public interface ISetupMesh
    {
        Type MeshType { get; }

        /// <summary>
        ///     May replace <see cref="IPrimitiveMesh.Mesh" />, so read it only after this call
        /// </summary>
        void Execute(PBMeshRenderer pbRenderer, IPrimitiveMesh primitiveMesh);
    }
}
