using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class SpherePrimitive : IPrimitiveMesh
    {
        public Mesh PrimitiveMesh { get; }

        public SpherePrimitive()
        {
            var newMesh = new Mesh();
            SphereFactory.Create(ref newMesh);
            PrimitiveMesh = newMesh;
        }
    }
}