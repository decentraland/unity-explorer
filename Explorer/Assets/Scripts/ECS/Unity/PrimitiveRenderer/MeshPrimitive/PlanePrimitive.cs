using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class PlanePrimitive : IPrimitiveMesh
    {
        public Mesh PrimitiveMesh { get; }

        public PlanePrimitive()
        {
            var newMesh = new Mesh();
            PlaneFactory.Create(ref newMesh);
            PrimitiveMesh = newMesh;
        }
    }
}