using GLTFast;
using NUnit.Framework;
using UnityEngine;

namespace Loading.Tests
{
    public class ModelMetricsShould
    {
        [Test]
        public void SumEveryCount()
        {
            // Arrange
            var first = new ModelMetrics(100, 2, 3, 4);
            var second = new ModelMetrics(10, 1, 2, 3);

            // Act
            var total = first + second;

            // Assert
            Assert.AreEqual(110, total.Triangles);
            Assert.AreEqual(3, total.Materials);
            Assert.AreEqual(5, total.Textures);
            Assert.AreEqual(7, total.Meshes);
        }

        [Test]
        public void StartEmpty()
        {
            // Act
            var total = ModelMetrics.Empty + new ModelMetrics(1, 2, 3, 4);

            // Assert
            Assert.AreEqual(1, total.Triangles);
            Assert.AreEqual(2, total.Materials);
            Assert.AreEqual(3, total.Textures);
            Assert.AreEqual(4, total.Meshes);
        }

        [Test]
        public void CountOnlyTheTexturesOfAFacialFeature()
        {
            // Arrange
            var main = new Texture2D(1, 1);
            var mask = new Texture2D(1, 1);

            try
            {
                // Act
                var withMask = ModelMetrics.FromTextures(main, mask);
                var withoutMask = ModelMetrics.FromTextures(main, null);

                // Assert
                Assert.AreEqual(2, withMask.Textures);
                Assert.AreEqual(1, withoutMask.Textures);
                Assert.AreEqual(0, withMask.Triangles);
                Assert.AreEqual(0, withMask.Materials);
                Assert.AreEqual(0, withMask.Meshes);
            }
            finally
            {
                Object.DestroyImmediate(main);
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void CountEverySubMeshAndSkipColliders()
        {
            // Arrange: one triangle per sub-mesh, on a body and on a collider sharing the mesh.
            var root = new GameObject("root");
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, subMeshCount = 2 };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 1 }, 1);

            var body = new GameObject("body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;

            var collider = new GameObject("body_Collider", typeof(MeshFilter), typeof(MeshRenderer));
            collider.transform.SetParent(root.transform);
            collider.GetComponent<MeshFilter>().sharedMesh = mesh;

            // Never loaded, so it has no source document: materials and textures read as none.
            using var importer = new GltfImport(deferAgent: new UninterruptedDeferAgent());

            try
            {
                // Act
                var metrics = ModelMetrics.Measure(root, importer);

                // Assert
                Assert.AreEqual(2, metrics.Triangles);
                Assert.AreEqual(2, metrics.Meshes);
                Assert.AreEqual(0, metrics.Materials);
                Assert.AreEqual(0, metrics.Textures);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
