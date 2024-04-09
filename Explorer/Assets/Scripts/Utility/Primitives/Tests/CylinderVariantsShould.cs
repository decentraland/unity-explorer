using NUnit.Framework;
using System;
using UnityEngine;

namespace Utility.Primitives.Tests
{
    public class CylinderVariantsShould
    {

        public void GenerateCylinder([Values(25, 50, 100, 300, 1000)] int verticesCount)
        {
            int finalVerticesCount = (verticesCount + 1) * 4;
            int trianglesCount = verticesCount * 12;

            var mesh = new Mesh();

            CylinderVariantsFactory.Create(ref mesh, numVertices: verticesCount);

            Assert.AreEqual(finalVerticesCount, mesh.vertexCount);
            Assert.AreEqual(finalVerticesCount, mesh.normals.Length);
            Assert.AreEqual(finalVerticesCount, mesh.uv.Length);
            Assert.AreEqual(trianglesCount, mesh.triangles.Length);
        }


        public void GenerateCone([Values(25, 50, 100, 300, 1000)] int verticesCount)
        {
            int finalVerticesCount = (verticesCount + 1) * 4;
            int trianglesCount = (verticesCount * 6) + ((verticesCount + 1) * 3);

            var mesh = new Mesh();

            CylinderVariantsFactory.Create(ref mesh, 0f, numVertices: verticesCount);

            Assert.AreEqual(finalVerticesCount, mesh.vertexCount);
            Assert.AreEqual(finalVerticesCount, mesh.normals.Length);
            Assert.AreEqual(finalVerticesCount, mesh.uv.Length);
            Assert.AreEqual(trianglesCount, mesh.triangles.Length);
        }


        public void GenerateTruncatedCone([Values(25, 50, 100, 300, 1000)] int verticesCount)
        {
            int finalVerticesCount = (verticesCount + 1) * 4;
            int trianglesCount = verticesCount * 12;

            var mesh = new Mesh();

            CylinderVariantsFactory.Create(ref mesh, 0.25f, numVertices: verticesCount);

            Assert.AreEqual(finalVerticesCount, mesh.vertexCount);
            Assert.AreEqual(finalVerticesCount, mesh.normals.Length);
            Assert.AreEqual(finalVerticesCount, mesh.uv.Length);
            Assert.AreEqual(trianglesCount, mesh.triangles.Length);
        }


        public void ReuseBuffers()
        {
            int finalVerticesCount = (CylinderVariantsFactory.VERTICES_NUM + 1) * 4;
            int trianglesCount = (CylinderVariantsFactory.VERTICES_NUM * 6) + (CylinderVariantsFactory.VERTICES_NUM * 6);

            int[] triangles = PrimitivesBuffersPool.TRIANGLES.Rent(trianglesCount);
            Vector2[] uvs = PrimitivesBuffersPool.UVS.Rent(finalVerticesCount);
            Vector3[] vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);
            Vector3[] normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(finalVerticesCount);

            PrimitivesBuffersPool.TRIANGLES.Return(triangles, true);
            PrimitivesBuffersPool.UVS.Return(uvs, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices, true);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals, true);

            var mesh = new Mesh();

            CylinderVariantsFactory.Create(ref mesh);

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
