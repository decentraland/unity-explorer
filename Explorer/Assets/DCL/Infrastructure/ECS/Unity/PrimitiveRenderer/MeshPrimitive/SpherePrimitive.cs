using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class SpherePrimitive : IPrimitiveMesh
    {
        public Mesh Mesh { get; }

        public SpherePrimitive()
        {
            var newMesh = new Mesh();
            SphereFactory.Create(ref newMesh);
            Mesh = newMesh;
        }
    }
}
