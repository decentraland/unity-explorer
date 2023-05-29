using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class BoxPrimitive : IPrimitiveMesh
    {
        public Mesh PrimitiveMesh { get; }

        public BoxPrimitive()
        {
            var newMesh = new Mesh();
            BoxFactory.Create(ref newMesh);
            PrimitiveMesh = newMesh;
        }
    }
}