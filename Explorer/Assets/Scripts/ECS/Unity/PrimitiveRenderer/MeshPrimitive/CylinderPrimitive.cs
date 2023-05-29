using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class CylinderPrimitive : IPrimitiveMesh
    {
        public Mesh PrimitiveMesh { get; }

        public CylinderPrimitive()
        {
            var newMesh = new Mesh();
            PrimitiveMesh = newMesh;
        }
    }
}