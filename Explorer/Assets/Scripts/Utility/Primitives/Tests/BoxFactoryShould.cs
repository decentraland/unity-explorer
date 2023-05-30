using System;
using Google.Protobuf.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Utility.Primitives.Tests
{
    [TestFixture]
    public class BoxFactoryShould
    {
        [Test]
        public void GenerateCorrectSize()
        {
            var mesh = new Mesh();
            var gameObject = new GameObject();
            gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();

            BoxFactory.Create(ref mesh);
            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.AreEqual(new Vector3(0.50f, 0.50f, 0.50f), meshRenderer.bounds.extents);
        }

        [Test]
        public void ValidateMeshCount()
        {
            var finalVerticesCount = BoxFactory.VERTICES_NUM;
            var trianglesCount = BoxFactory.TRIS_NUM;

            var mesh = new Mesh();
            BoxFactory.Create(ref mesh);

            Assert.AreEqual(mesh.vertices.Length, finalVerticesCount);
            Assert.AreEqual(mesh.normals.Length, finalVerticesCount);
            Assert.AreEqual(mesh.uv.Length, finalVerticesCount);
            Assert.AreEqual(mesh.triangles.Length, trianglesCount);
        }

        [Test]
        public void UpdateUVS()
        {
            var mesh = new Mesh();
            BoxFactory.Create(ref mesh);

            var originalUVs = mesh.uv;

            BoxFactory.UpdateMesh(ref mesh);
            Assert.AreEqual(mesh.uv, originalUVs);

            var repeatedField = new RepeatedField<float>();
            for (var i = 0; i < BoxFactory.VERTICES_NUM; i++)
            {
                repeatedField.Add(i);
            }

            BoxFactory.UpdateMesh(ref mesh, repeatedField);
            Assert.AreNotEqual(mesh.uv, originalUVs);
        }

        [Test]
        public void ReuseBuffers()
        {
            var finalVerticesCount = BoxFactory.VERTICES_NUM;
            var trianglesCount = BoxFactory.TRIS_NUM;

            var triangles = PrimitivesBuffersPool.TRIANGLES.Rent(trianglesCount);
            var uvs = PrimitivesBuffersPool.UVS.Rent(finalVerticesCount);
            var vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);
            var normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);

            PrimitivesBuffersPool.TRIANGLES.Return(triangles, true);
            PrimitivesBuffersPool.UVS.Return(uvs, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals, true);

            var mesh = new Mesh();

            BoxFactory.Create(ref mesh);

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