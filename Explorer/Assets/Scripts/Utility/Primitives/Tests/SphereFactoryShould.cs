using System;
using NUnit.Framework;
using UnityEngine;

namespace Utility.Primitives.Tests
{
    [TestFixture]
    public class SphereFactoryShould
    {
        [Test]
        public void GenerateCorrectSize()
        {
            var mesh = new Mesh();
            var gameObject = new GameObject();
            gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();

            SphereFactory.Create(ref mesh);
            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.Less((meshRenderer.bounds.extents - new Vector3(0.50f, 0.50f, 0.50f)).magnitude, 0.005f);
        }

        [Test]
        public void ValidateMeshCount()
        {
            var finalVerticesCount = (SphereFactory.LONGITUDE + 1) * SphereFactory.LATITUDE + 2;
            var trianglesCount = finalVerticesCount * 6;

            var mesh = new Mesh();
            SphereFactory.Create(ref mesh);

            Assert.AreEqual(mesh.vertices.Length, finalVerticesCount);
            Assert.AreEqual(mesh.normals.Length, finalVerticesCount);
            Assert.AreEqual(mesh.uv.Length, finalVerticesCount);
            Assert.AreEqual(mesh.triangles.Length, trianglesCount);
        }

        [Test]
        public void ReuseBuffers()
        {
            var finalVerticesCount = (SphereFactory.LONGITUDE + 1) * SphereFactory.LATITUDE + 2;
            var trianglesCount = finalVerticesCount * 6;

            var triangles = PrimitivesBuffersPool.TRIANGLES.Rent(trianglesCount);
            var uvs = PrimitivesBuffersPool.UVS.Rent(finalVerticesCount);
            var vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);
            var normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);

            PrimitivesBuffersPool.TRIANGLES.Return(triangles, true);
            PrimitivesBuffersPool.UVS.Return(uvs, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals, true);

            var mesh = new Mesh();

            SphereFactory.Create(ref mesh);

            // We don't clear buffer so if they are reused they should be filled with data

            void CheckAllItemsAreNotDefault<T>(string arrayName, T[] array) where T : IEquatable<T>
            {
                foreach (var element in array)
                {
                    if (!element.Equals(default))
                        return;
                }

                Assert.Fail($"{arrayName} was not used");
            }

            CheckAllItemsAreNotDefault("triangles", triangles);
            CheckAllItemsAreNotDefault("uvs", uvs);
            CheckAllItemsAreNotDefault("vertices", vertices);
            CheckAllItemsAreNotDefault("normals", normals);
        }
    }
}