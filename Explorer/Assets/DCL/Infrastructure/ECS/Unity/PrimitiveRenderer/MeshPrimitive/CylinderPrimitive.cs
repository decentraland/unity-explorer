using UnityEngine;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class CylinderPrimitive : IPrimitiveMesh
    {
        public Mesh Mesh { get; }

        public CylinderPrimitive()
        {
            Mesh = new Mesh();
        }
    }
}
