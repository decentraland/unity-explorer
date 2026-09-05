using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class PlanePrimitive : IPrimitiveMesh
    {
        public Mesh Mesh { get; }

        public PlanePrimitive()
        {
            var newMesh = new Mesh();
            PlaneFactory.Create(ref newMesh);
            Mesh = newMesh;
        }
    }
}
