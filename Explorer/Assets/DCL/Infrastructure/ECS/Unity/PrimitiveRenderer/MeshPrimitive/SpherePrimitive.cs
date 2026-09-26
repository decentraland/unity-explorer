using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class SpherePrimitive : IPrimitiveMesh
    {
        // Sphere geometry is constant (fixed radius and tessellation) and is never mutated per instance
        // (MeshSetupSphere.Execute is a no-op), so every sphere shares this single immutable mesh.
        private static Mesh? sharedMesh;

        public Mesh Mesh { get; }

        public SpherePrimitive()
        {
            Mesh = sharedMesh ??= CreateSharedMesh();
        }

        private static Mesh CreateSharedMesh()
        {
            var newMesh = new Mesh();
            SphereFactory.Create(ref newMesh);
            return newMesh;
        }
    }
}
