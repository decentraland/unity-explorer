using Google.Protobuf.Collections;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Utility.Primitives.Tests
{

    public class BoxFactoryShould
    {

        public void GenerateCorrectSize()
        {
            var mesh = new Mesh();
            var gameObject = new GameObject();
            gameObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();

            BoxFactory.Create(ref mesh);
            meshRenderer.GetComponent<MeshFilter>().mesh = mesh;
            Assert.AreEqual(new Vector3(0.50f, 0.50f, 0.50f), meshRenderer.bounds.extents);
        }


        public void ValidateMeshCount()
        {
            int finalVerticesCount = BoxFactory.VERTICES_NUM;
            int trianglesCount = BoxFactory.TRIS_NUM;

            var mesh = new Mesh();
            BoxFactory.Create(ref mesh);

            Assert.AreEqual(mesh.vertices.Length, finalVerticesCount);
            Assert.AreEqual(mesh.normals.Length, finalVerticesCount);
            Assert.AreEqual(mesh.uv.Length, finalVerticesCount);
            Assert.AreEqual(mesh.triangles.Length, trianglesCount);
        }


        public void UpdateUVS()
        {
            var mesh = new Mesh();
            BoxFactory.Create(ref mesh);

            Vector2[] originalUVs = mesh.uv;

            BoxFactory.UpdateMesh(ref mesh);
            Assert.AreEqual(mesh.uv, originalUVs);

            var repeatedField = new RepeatedField<float>();

            for (var i = 0; i < BoxFactory.VERTICES_NUM; i++) { repeatedField.Add(i); }

            BoxFactory.UpdateMesh(ref mesh, repeatedField);
            Assert.AreNotEqual(mesh.uv, originalUVs);
        }


        public void ReuseBuffers()
        {
            int finalVerticesCount = BoxFactory.VERTICES_NUM;
            int trianglesCount = BoxFactory.TRIS_NUM;

            int[] triangles = PrimitivesBuffersPool.TRIANGLES.Rent(trianglesCount);
            Vector2[] uvs = PrimitivesBuffersPool.UVS.Rent(finalVerticesCount);
            Vector3[] vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);
            Vector3[] normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);

            PrimitivesBuffersPool.TRIANGLES.Return(triangles, true);
            PrimitivesBuffersPool.UVS.Return(uvs, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals, true);

            var mesh = new Mesh();

            BoxFactory.Create(ref mesh);

            // We don't clear buffer so if they are reused they should be filled with data

            void CheckAllItemsAreNotDefault<T>(string arrayName, T[] array) where T: IEquatable<T>
            {
                foreach (T element in array)
                {
                    if (!element.Equals(default(T)))
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
