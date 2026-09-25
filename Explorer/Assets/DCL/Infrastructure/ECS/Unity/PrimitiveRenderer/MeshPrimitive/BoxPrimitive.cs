using Google.Protobuf.Collections;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.MeshPrimitive
{
    public class BoxPrimitive : IPrimitiveMesh
    {
        // Box geometry is constant; only UV channel 0 can vary per instance. Boxes with default UVs share this
        // single immutable mesh, a box with custom UVs switches to its own mesh (kept for reuse across pool rents).
        private static Mesh? sharedMesh;

        private Mesh? ownMesh;

        private static Mesh SharedMesh => sharedMesh ??= CreateMesh();

        public Mesh Mesh { get; private set; }

        public BoxPrimitive()
        {
            Mesh = SharedMesh;
        }

        public void ApplyUVs(RepeatedField<float>? uvs)
        {
            if (uvs is not { Count: > 0 })
            {
                Mesh = SharedMesh;
                return;
            }

            ownMesh ??= CreateMesh();
            BoxFactory.UpdateMesh(ref ownMesh, uvs);
            Mesh = ownMesh;
        }

        private static Mesh CreateMesh()
        {
            var newMesh = new Mesh();
            BoxFactory.Create(ref newMesh);
            return newMesh;
        }
    }
}
