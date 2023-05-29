using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public interface IPrimitiveMesh
    {
        Mesh PrimitiveMesh { get; }
    }
}