using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class CylinderPrimitive : IPrimitiveMesh
    {
        public Mesh Mesh { get; }

        public CylinderPrimitive()
        {
            var newMesh = new Mesh();
            Mesh = newMesh;
        }
    }
}