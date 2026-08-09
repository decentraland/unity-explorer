using DCL.AvatarRendering.AvatarShape.ComputeShader;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Tests
{
    /// <summary>
    /// Pins the per-shared-Mesh memoization of <see cref="ComputeShaderSkinning.EnsureTangents"/>: tangents are
    /// (re)computed once per distinct asset-bundle Mesh reference, and every later avatar build of that same
    /// shared Mesh short-circuits on the by-reference guard set instead of recomputing.
    /// </summary>
    public class ComputeShaderSkinningTangentsShould
    {
        private static Mesh BuildGridMesh(int targetVerts)
        {
            int side = Mathf.CeilToInt(Mathf.Sqrt(targetVerts));

            var vertices = new Vector3[side * side];
            var normals = new Vector3[side * side];
            var uvs = new Vector2[side * side];

            for (var y = 0; y < side; y++)
            for (var x = 0; x < side; x++)
            {
                int idx = (y * side) + x;
                vertices[idx] = new Vector3(x, y, 0);
                normals[idx] = Vector3.forward;
                uvs[idx] = new Vector2((float)x / side, (float)y / side);
            }

            var tris = new List<int>((side - 1) * (side - 1) * 6);

            for (var y = 0; y < side - 1; y++)
            for (var x = 0; x < side - 1; x++)
            {
                int i0 = (y * side) + x;
                int i1 = i0 + 1;
                int i2 = i0 + side;
                int i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            return mesh;
        }

        [Test]
        public void RecomputeOncePerSharedMesh()
        {
            Mesh mesh = BuildGridMesh(64_000);
            var skinning = new ComputeShaderSkinning();

            // Test-visible recompute counter, incremented only when EnsureTangents actually recomputed.
            var recomputes = 0;

            // Pass 1: first sight of the shared asset-bundle Mesh -> tangents computed.
            if (skinning.EnsureTangents(mesh)) recomputes++;
            Assert.That(skinning.tangentsGenerated.Contains(mesh), Is.True,
                "the by-reference guard must record the shared Mesh after the first build");

            // Pass 2: the SAME shared Mesh instance (another avatar wearing the same item) -> memo short-circuits.
            if (skinning.EnsureTangents(mesh)) recomputes++;

            Assert.That(recomputes, Is.EqualTo(1),
                "tangents must be recomputed exactly once across two builds of the same shared Mesh");

            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void RecomputeAgainForADistinctMesh()
        {
            var skinning = new ComputeShaderSkinning();

            Mesh a = BuildGridMesh(1_024);
            Mesh b = BuildGridMesh(1_024);

            // Keyed by Mesh identity, NOT avatar/instance: each distinct asset-bundle Mesh must still compute once,
            // so facial/skin meshes are never skipped just because a different mesh was seen earlier.
            Assert.That(skinning.EnsureTangents(a), Is.True, "first distinct mesh must compute");
            Assert.That(skinning.EnsureTangents(b), Is.True, "a different Mesh instance must compute, not be skipped");
            Assert.That(skinning.EnsureTangents(a), Is.False, "re-seeing the first mesh must short-circuit");

            UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b);
        }
    }
}
