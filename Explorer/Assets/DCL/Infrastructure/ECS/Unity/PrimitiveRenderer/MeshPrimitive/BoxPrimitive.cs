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

        // DO NOT COMMIT (JUANI): temporary counters to confirm shared-mesh reuse in build
        private static int sharedUses;
        private static int ownMeshesCreated;

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
                sharedUses++;
                Debug.Log($"JUANI BoxPrimitive shared mesh reused (id {Mesh.GetInstanceID()}), shared uses: {sharedUses}, own meshes: {ownMeshesCreated}");
                return;
            }

            if (ownMesh == null)
            {
                ownMesh = CreateMesh();
                ownMeshesCreated++;
                Debug.Log($"JUANI BoxPrimitive own mesh created for custom UVs, shared uses: {sharedUses}, own meshes: {ownMeshesCreated}");
            }
            else
                Debug.Log($"JUANI BoxPrimitive own mesh reused for custom UVs (id {ownMesh.GetInstanceID()})");
            BoxFactory.UpdateMesh(ref ownMesh, uvs);
            Mesh = ownMesh;
        }

        private static Mesh CreateMesh()
        {
            Debug.Log("JUANI BoxPrimitive CreateMesh");
            var newMesh = new Mesh();
            BoxFactory.Create(ref newMesh);
            return newMesh;
        }
    }
}
