using Google.Protobuf.Collections;
using UnityEngine;

namespace Utility.Primitives
{
    public static class BoxFactory
    {
        internal const int VERTICES_NUM = 24;
        internal const int TRIS_NUM = 36;

        private static Vector2[] defaultUVs;

        public static void Create(ref Mesh mesh)
        {
            mesh.name = "DCL Box";

            Vector3[] vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(VERTICES_NUM); //top bottom left right front back
            Vector3[] normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(VERTICES_NUM);
            defaultUVs = new Vector2[VERTICES_NUM];
            Vector2[] uvs2 = PrimitivesBuffersPool.UVS.Rent(VERTICES_NUM);
            int[] tris = PrimitivesBuffersPool.TRIANGLES.Rent(TRIS_NUM);

            var vIndex = 0;

            float size = PrimitivesSize.CUBE_SIZE;

            //top and bottom
            var start = new Vector3(-size / 2, size / 2, size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.right * size);
            vertices[vIndex++] = start + (Vector3.right * size) + (Vector3.back * size);
            vertices[vIndex++] = start + (Vector3.back * size);

            start = new Vector3(-size / 2, -size / 2, size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.right * size);
            vertices[vIndex++] = start + (Vector3.right * size) + (Vector3.back * size);
            vertices[vIndex++] = start + (Vector3.back * size);

            //left and right
            start = new Vector3(-size / 2, size / 2, size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.back * size);
            vertices[vIndex++] = start + (Vector3.back * size) + (Vector3.down * size);
            vertices[vIndex++] = start + (Vector3.down * size);

            start = new Vector3(size / 2, size / 2, size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.back * size);
            vertices[vIndex++] = start + (Vector3.back * size) + (Vector3.down * size);
            vertices[vIndex++] = start + (Vector3.down * size);

            //front and back
            start = new Vector3(-size / 2, size / 2, size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.right * size);
            vertices[vIndex++] = start + (Vector3.right * size) + (Vector3.down * size);
            vertices[vIndex++] = start + (Vector3.down * size);

            start = new Vector3(-size / 2, size / 2, -size / 2);
            vertices[vIndex++] = start;
            vertices[vIndex++] = start + (Vector3.right * size);
            vertices[vIndex++] = start + (Vector3.right * size) + (Vector3.down * size);
            vertices[vIndex++] = start + (Vector3.down * size);

            //uv
            var uvIndex = 0;

            //top and bottom
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);
            defaultUVs[uvIndex++] = new Vector2(0f, 1f);

            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(0f, 1f);
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);

            //left and right
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);
            defaultUVs[uvIndex++] = new Vector2(0f, 1f);

            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(0f, 1f);
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);

            //front and back
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);
            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(0f, 1f);

            defaultUVs[uvIndex++] = new Vector2(0f, 1f);
            defaultUVs[uvIndex++] = new Vector2(1f, 1f);
            defaultUVs[uvIndex++] = new Vector2(1f, 0f);
            defaultUVs[uvIndex++] = new Vector2(0f, 0f);

            //uv2
            vIndex = 0;

            //top and bottom
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(0f, 0f);
            uvs2[vIndex++] = new Vector2(0f, 1f);

            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(0f, 1f);
            uvs2[vIndex++] = new Vector2(0f, 0f);

            //left and right
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(0f, 0f);
            uvs2[vIndex++] = new Vector2(0f, 1f);

            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(0f, 1f);
            uvs2[vIndex++] = new Vector2(0f, 0f);

            //front and back
            uvs2[vIndex++] = new Vector2(0f, 0f);
            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(0f, 1f);

            uvs2[vIndex++] = new Vector2(0f, 1f);
            uvs2[vIndex++] = new Vector2(1f, 1f);
            uvs2[vIndex++] = new Vector2(1f, 0f);
            uvs2[vIndex++] = new Vector2(0f, 0f);

            //normal
            vIndex = 0;

            //top and bottom
            normals[vIndex++] = Vector3.up;
            normals[vIndex++] = Vector3.up;
            normals[vIndex++] = Vector3.up;
            normals[vIndex++] = Vector3.up;

            normals[vIndex++] = Vector3.down;
            normals[vIndex++] = Vector3.down;
            normals[vIndex++] = Vector3.down;
            normals[vIndex++] = Vector3.down;

            //left and right
            normals[vIndex++] = Vector3.left;
            normals[vIndex++] = Vector3.left;
            normals[vIndex++] = Vector3.left;
            normals[vIndex++] = Vector3.left;

            normals[vIndex++] = Vector3.right;
            normals[vIndex++] = Vector3.right;
            normals[vIndex++] = Vector3.right;
            normals[vIndex++] = Vector3.right;

            //front and back
            normals[vIndex++] = Vector3.forward;
            normals[vIndex++] = Vector3.forward;
            normals[vIndex++] = Vector3.forward;
            normals[vIndex++] = Vector3.forward;

            normals[vIndex++] = Vector3.back;
            normals[vIndex++] = Vector3.back;
            normals[vIndex++] = Vector3.back;
            normals[vIndex++] = Vector3.back;

            var cnt = 0;

            //top and bottom
            tris[cnt++] = 0;
            tris[cnt++] = 1;
            tris[cnt++] = 2;
            tris[cnt++] = 0;
            tris[cnt++] = 2;
            tris[cnt++] = 3;

            tris[cnt++] = 4 + 0;
            tris[cnt++] = 4 + 2;
            tris[cnt++] = 4 + 1;
            tris[cnt++] = 4 + 0;
            tris[cnt++] = 4 + 3;
            tris[cnt++] = 4 + 2;

            //left and right
            tris[cnt++] = 8 + 0;
            tris[cnt++] = 8 + 1;
            tris[cnt++] = 8 + 2;
            tris[cnt++] = 8 + 0;
            tris[cnt++] = 8 + 2;
            tris[cnt++] = 8 + 3;

            tris[cnt++] = 12 + 0;
            tris[cnt++] = 12 + 2;
            tris[cnt++] = 12 + 1;
            tris[cnt++] = 12 + 0;
            tris[cnt++] = 12 + 3;
            tris[cnt++] = 12 + 2;

            //front and back
            tris[cnt++] = 16 + 0;
            tris[cnt++] = 16 + 2;
            tris[cnt++] = 16 + 1;
            tris[cnt++] = 16 + 0;
            tris[cnt++] = 16 + 3;
            tris[cnt++] = 16 + 2;

            tris[cnt++] = 20 + 0;
            tris[cnt++] = 20 + 1;
            tris[cnt++] = 20 + 2;
            tris[cnt++] = 20 + 0;
            tris[cnt++] = 20 + 2;
            tris[cnt++] = 20 + 3;

            mesh.SetVertices(vertices, 0, VERTICES_NUM);
            mesh.SetNormals(normals, 0, VERTICES_NUM);
            mesh.SetUVs(0, defaultUVs, 0, VERTICES_NUM);
            mesh.SetUVs(1, uvs2, 0, VERTICES_NUM);
            mesh.SetTriangles(tris, 0, TRIS_NUM, 0);

            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals);
            PrimitivesBuffersPool.UVS.Return(uvs2);
            PrimitivesBuffersPool.TRIANGLES.Return(tris);
        }

        public static void UpdateMesh(ref Mesh mesh, RepeatedField<float> boxUVs = null)
        {
            if (boxUVs is { Count: > 0 })
                mesh.SetUVs(0, PrimitivesUtility.FloatArrayToV2List(boxUVs, mesh.uv), 0, VERTICES_NUM);
            else
                mesh.SetUVs(0, defaultUVs, 0, VERTICES_NUM);
        }
    }
}
