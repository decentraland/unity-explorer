using UnityEngine;
using Utility.Primitives;

public static class PlaneFactory
{
    public const int VERTICES_NUM = 8;
    public const int TRIS_NUM = 12;
    public static readonly Vector3 HALF_SIZE = new (0.5f, 0.5f, 0);

    // Creates a two-sided quad (clockwise)
    public static void Create(ref Mesh mesh)
    {
        mesh.name = "DCL Plane";

        Vector3[] vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(VERTICES_NUM);
        ;
        vertices[0] = new Vector3(-HALF_SIZE.x, -HALF_SIZE.y, 0);
        vertices[1] = new Vector3(-HALF_SIZE.x, HALF_SIZE.y, 0);
        vertices[2] = new Vector3(HALF_SIZE.x, HALF_SIZE.y, 0);
        vertices[3] = new Vector3(HALF_SIZE.x, -HALF_SIZE.y, 0);

        vertices[4] = new Vector3(HALF_SIZE.x, -HALF_SIZE.y, 0);
        vertices[5] = new Vector3(HALF_SIZE.x, HALF_SIZE.y, 0);
        vertices[6] = new Vector3(-HALF_SIZE.x, HALF_SIZE.y, 0);
        vertices[7] = new Vector3(-HALF_SIZE.x, -HALF_SIZE.y, 0);

        Vector2[] uvs = PrimitivesBuffersPool.UVS.Rent(VERTICES_NUM);
        uvs[0] = new Vector2(0f, 0f);
        uvs[1] = new Vector2(0f, 1f);
        uvs[2] = new Vector2(1f, 1f);
        uvs[3] = new Vector2(1f, 0f);

        uvs[4] = new Vector2(1f, 0f);
        uvs[5] = new Vector2(1f, 1f);
        uvs[6] = new Vector2(0f, 1f);
        uvs[7] = new Vector2(0f, 0f);

        int[] tris = PrimitivesBuffersPool.TRIANGLES.Rent(TRIS_NUM);
        tris[0] = 0;
        tris[1] = 1;
        tris[2] = 2;
        tris[3] = 2;
        tris[4] = 3;
        tris[5] = 0;

        tris[6] = 4;
        tris[7] = 5;
        tris[8] = 6;
        tris[9] = 6;
        tris[10] = 7;
        tris[11] = 4;

        Vector3[] normals = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(VERTICES_NUM);
        normals[0] = Vector3.back;
        normals[1] = Vector3.back;
        normals[2] = Vector3.back;
        normals[3] = Vector3.back;

        normals[4] = Vector3.forward;
        normals[5] = Vector3.forward;
        normals[6] = Vector3.forward;
        normals[7] = Vector3.forward;

        var colors = new Color[8];

        for (var i = 0; i < colors.Length; i++) { colors[i] = Color.white; }

        mesh.SetVertices(vertices, 0, VERTICES_NUM);
        mesh.SetNormals(normals, 0, VERTICES_NUM);
        mesh.SetUVs(0, uvs, 0, VERTICES_NUM);
        mesh.SetTriangles(tris, 0, TRIS_NUM, 0);

        mesh.colors = colors;

        PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices);
        ;
        PrimitivesBuffersPool.UVS.Return(uvs);
        PrimitivesBuffersPool.TRIANGLES.Return(tris);
        PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normals);
    }
}
