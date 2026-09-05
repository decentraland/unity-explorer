using UnityEngine;

namespace Utility.Primitives
{
    public class SphereFactory : MonoBehaviour
    {
        internal const int LONGITUDE = 24;
        internal const int LATITUDE = 16;

        public static void Create(ref Mesh mesh)
        {
            mesh.name = "DCL Sphere";
            float radius = PrimitivesSize.SPHERE_RADIUS;

            //float radius = 1f;
            // Longitude |||
            int nbLong = LONGITUDE;

            // Latitude ---
            int nbLat = LATITUDE;

#region Vertices
            int verticesLength = ((nbLong + 1) * nbLat) + 2;

            Vector3[] vertices = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(verticesLength);
            float _pi = Mathf.PI;
            float _2pi = _pi * 2f;

            vertices[0] = Vector3.up * radius;

            for (var lat = 0; lat < nbLat; lat++)
            {
                float a1 = _pi * (lat + 1) / (nbLat + 1);
                float sin1 = Mathf.Sin(a1);
                float cos1 = Mathf.Cos(a1);

                for (var lon = 0; lon <= nbLong; lon++)
                {
                    float a2 = _2pi * (lon == nbLong ? 0 : lon) / nbLong;
                    float sin2 = Mathf.Sin(a2);
                    float cos2 = Mathf.Cos(a2);

                    vertices[lon + (lat * (nbLong + 1)) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
                }
            }

            vertices[verticesLength - 1] = Vector3.up * -radius;
#endregion

#region Normales
            Vector3[] normales = PrimitivesBuffersPool.EQUAL_TO_VERTICES.Rent(verticesLength);

            for (var n = 0; n < verticesLength; n++) { normales[n] = vertices[n].normalized; }
#endregion

#region UVs
            Vector2[] uvs = PrimitivesBuffersPool.UVS.Rent(verticesLength);
            uvs[0] = Vector2.up;
            uvs[uvs.Length - 1] = Vector2.zero;

            for (var lat = 0; lat < nbLat; lat++)
            {
                for (var lon = 0; lon <= nbLong; lon++)
                {
                    uvs[lon + (lat * (nbLong + 1)) + 1] =
                        new Vector2(1f - ((float)lon / nbLong), (float)(lat + 1) / (nbLat + 1));
                }
            }
#endregion

#region Triangles
            int nbFaces = verticesLength;
            int nbTriangles = nbFaces * 2;
            int nbIndexes = nbTriangles * 3;
            int[] triangles = PrimitivesBuffersPool.TRIANGLES.Rent(nbIndexes);

            for (var j = 0; j < nbIndexes; j++) { triangles[j] = 0; }

            //Top Cap
            var i = 0;

            for (var lon = 0; lon < nbLong; lon++)
            {
                triangles[i++] = lon + 2;
                triangles[i++] = lon + 1;
                triangles[i++] = 0;
            }

            //Middle
            for (var lat = 0; lat < nbLat - 1; lat++)
            {
                for (var lon = 0; lon < nbLong; lon++)
                {
                    int current = lon + (lat * (nbLong + 1)) + 1;
                    int next = current + nbLong + 1;

                    triangles[i++] = current;
                    triangles[i++] = current + 1;
                    triangles[i++] = next + 1;

                    triangles[i++] = current;
                    triangles[i++] = next + 1;
                    triangles[i++] = next;
                }
            }

            //Bottom Cap
            for (var lon = 0; lon < nbLong; lon++)
            {
                triangles[i++] = verticesLength - 1;
                triangles[i++] = verticesLength - (lon + 2) - 1;
                triangles[i++] = verticesLength - (lon + 1) - 1;
            }
#endregion

            mesh.SetVertices(vertices, 0, verticesLength);
            mesh.SetNormals(normales, 0, verticesLength);
            mesh.SetUVs(0, uvs, 0, verticesLength);
            mesh.SetTriangles(triangles, 0, nbIndexes, 0);
            mesh.RecalculateBounds();

            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(vertices);
            PrimitivesBuffersPool.EQUAL_TO_VERTICES.Return(normales);
            PrimitivesBuffersPool.UVS.Return(uvs);
            PrimitivesBuffersPool.TRIANGLES.Return(triangles);
        }
    }
}
