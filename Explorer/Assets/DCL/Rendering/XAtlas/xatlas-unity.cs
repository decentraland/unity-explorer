using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

namespace XAtlasSharp
{
    /// <summary>
    /// C# version of xatlas for Unity, providing functionality for generating texture atlas UVs for 3D meshes.
    /// </summary>
    public class XAtlas
    {
        #region Enums
        /// <summary>
        /// Types of chart parameterization methods
        /// </summary>
        public enum ChartType
        {
            Planar,  // Planar projection
            Ortho,   // Orthogonal parameterization
            LSCM,    // Least Squares Conformal Maps
            Piecewise, // Piecewise parameterization
            Invalid   // Invalid parameterization
        }

        /// <summary>
        /// Index formats used in mesh declarations
        /// </summary>
        public enum IndexFormat
        {
            UInt16,
            UInt32
        }

        /// <summary>
        /// Possible errors when adding a mesh
        /// </summary>
        public enum AddMeshError
        {
            Success, // No error
            Error,   // Unspecified error
            IndexOutOfRange, // An index is >= MeshDecl vertexCount
            InvalidFaceVertexCount, // Must be >= 3
            InvalidIndexCount // Not evenly divisible by 3 - expecting triangles
        }

        /// <summary>
        /// Progress tracking categories
        /// </summary>
        public enum ProgressCategory
        {
            AddMesh,
            ComputeCharts,
            PackCharts,
            BuildOutputMeshes
        }
        #endregion

        #region Data Structures
        /// <summary>
        /// A group of connected faces, belonging to a single atlas
        /// </summary>
        public class Chart
        {
            public int[] faceArray;      // Indices of faces in this chart
            public int atlasIndex;       // Sub-atlas index
            public int faceCount;        // Number of faces
            public ChartType type;       // Type of chart parameterization
            public int material;         // Material index
        }

        /// <summary>
        /// Output vertex data
        /// </summary>
        public class Vertex
        {
            public int atlasIndex;       // Sub-atlas index. -1 if the vertex doesn't exist in any atlas
            public int chartIndex;       // -1 if the vertex doesn't exist in any chart
            public Vector2 uv;           // Not normalized - values are in Atlas width and height range
            public int xref;             // Index of input vertex from which this output vertex originated
        }

        /// <summary>
        /// Output mesh data
        /// </summary>
        public class Mesh
        {
            public Chart[] chartArray;   // Array of charts
            public int[] indexArray;     // Index array
            public Vertex[] vertexArray; // Vertex array
            public int chartCount;       // Number of charts
            public int indexCount;       // Number of indices
            public int vertexCount;      // Number of vertices
        }

        /// <summary>
        /// Represents an entire texture atlas
        /// </summary>
        public class Atlas
        {
            public Color32[] image;      // Atlas image data (if created)
            public Mesh[] meshes;        // The output meshes, corresponding to each AddMesh call
            public float[] utilization;  // Normalized atlas texel utilization array
            public int width;            // Atlas width in texels
            public int height;           // Atlas height in texels
            public int atlasCount;       // Number of sub-atlases
            public int chartCount;       // Total number of charts in all meshes
            public int meshCount;        // Number of output meshes
            public float texelsPerUnit;  // Texels per unit ratio
        }

        /// <summary>
        /// Input mesh declaration
        /// </summary>
        public class MeshDecl
        {
            public Vector3[] vertexPositionData = null;
            public Vector3[] vertexNormalData = null;    // optional
            public Vector2[] vertexUvData = null;        // optional
            public int[] indexData = null;               // optional

            // Optional. Don't atlas faces set to true
            public bool[] faceIgnoreData = null;

            // Optional. Only faces with the same material will be assigned to the same chart
            public int[] faceMaterialData = null;

            // Optional. Polygon / n-gon support. Faces are assumed to be triangles if this is null
            public byte[] faceVertexCount = null;

            public int vertexCount = 0;
            public int indexCount = 0;
            public int indexOffset = 0;  // optional
            public int faceCount = 0;    // Optional if faceVertexCount is null
            public IndexFormat indexFormat = IndexFormat.UInt16;

            // Vertex positions within epsilon distance of each other are considered colocal
            public float epsilon = 1.192092896e-07f;
        }

        /// <summary>
        /// Input UV mesh declaration
        /// </summary>
        public class UvMeshDecl
        {
            public Vector2[] vertexUvData = null;
            public int[] indexData = null;                // optional
            public int[] faceMaterialData = null;         // Optional
            public int vertexCount = 0;
            public int indexCount = 0;
            public int indexOffset = 0;                   // optional
            public IndexFormat indexFormat = IndexFormat.UInt16;
        }

        /// <summary>
        /// Configuration options for chart computation
        /// </summary>
        public class ChartOptions
        {
            // Function pointer for custom parameterization
            public Func<Vector3[], Vector2[], int, int[], int, bool> paramFunc = null;

            public float maxChartArea = 0.0f;            // Don't grow charts to be larger than this. 0 means no limit
            public float maxBoundaryLength = 0.0f;       // Don't grow charts to have a longer boundary than this. 0 means no limit

            // Weights determine chart growth. Higher weights mean higher cost for that metric
            public float normalDeviationWeight = 2.0f;   // Angle between face and average chart normal
            public float roundnessWeight = 0.01f;
            public float straightnessWeight = 6.0f;
            public float normalSeamWeight = 4.0f;        // If > 1000, normal seams are fully respected
            public float textureSeamWeight = 0.5f;

            public float maxCost = 2.0f;                 // Lower values result in more charts
            public int maxIterations = 1;                // Higher values result in better charts

            public bool useInputMeshUvs = false;         // Use MeshDecl::vertexUvData for charts
            public bool fixWinding = false;              // Enforce consistent texture coordinate winding
        }

        /// <summary>
        /// Configuration options for chart packing
        /// </summary>
        public class PackOptions
        {
            // Charts larger than this will be scaled down. 0 means no limit
            public int maxChartSize = 0;

            // Number of pixels to pad charts with
            public int padding = 0;

            // Unit to texel scale. If 0, an estimated value will be used
            public float texelsPerUnit = 0.0f;

            // If 0, generate a single atlas with texelsPerUnit determining the final resolution
            public int resolution = 0;

            // Leave space for bilinear filtering
            public bool bilinear = true;

            // Align charts to 4x4 blocks
            public bool blockAlign = false;

            // Slower, but gives the best result. If false, use random chart placement
            public bool bruteForce = false;

            // Create Atlas::image
            public bool createImage = false;

            // Rotate charts to the axis of their convex hull
            public bool rotateChartsToAxis = true;

            // Rotate charts to improve packing
            public bool rotateCharts = true;
        }
        #endregion

        /// <summary>
        /// Progress callback delegate
        /// </summary>
        public delegate bool ProgressCallback(ProgressCategory category, int progress);

        #region Atlas Generation Core
        // Internal context for atlas generation
        private class Context
        {
            public Atlas atlas = new Atlas();
            public List<UnityEngine.Mesh> inputMeshes = new List<UnityEngine.Mesh>();
            public List<MeshBuilder> meshBuilders = new List<MeshBuilder>();
            public ProgressCallback progressCallback = null;
            public CancellationTokenSource cancellationSource = new CancellationTokenSource();
        }

        // The current context
        private Context _context;

        // Chart parameterization strategies
        private interface IChartParameterizer
        {
            bool Parameterize(List<Vector3> positions, List<int> indices, List<Vector2> outTexCoords);
        }

        // Orthogonal chart parameterization
        private class OrthoParameterizer : IChartParameterizer
        {
            public bool Parameterize(List<Vector3> positions, List<int> indices, List<Vector2> outTexCoords)
            {
                // Compute a basis for projection
                Vector3 normal = ComputeNormal(positions, indices);
                Vector3 tangent = ComputeTangent(normal);
                Vector3 bitangent = Vector3.Cross(normal, tangent);

                // Project each vertex onto the plane
                for (int i = 0; i < positions.Count; i++)
                {
                    Vector3 pos = positions[i];
                    outTexCoords[i] = new Vector2(
                        Vector3.Dot(tangent, pos),
                        Vector3.Dot(bitangent, pos)
                    );
                }

                return true;
            }

            private Vector3 ComputeNormal(List<Vector3> positions, List<int> indices)
            {
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < indices.Count; i += 3)
                {
                    Vector3 p0 = positions[indices[i]];
                    Vector3 p1 = positions[indices[i + 1]];
                    Vector3 p2 = positions[indices[i + 2]];
                    normal += Vector3.Cross(p1 - p0, p2 - p0);
                }
                return normal.normalized;
            }

            private Vector3 ComputeTangent(Vector3 normal)
            {
                Vector3 tangent;
                if (Mathf.Abs(normal.x) < Mathf.Abs(normal.y) && Mathf.Abs(normal.x) < Mathf.Abs(normal.z))
                    tangent = new Vector3(1, 0, 0);
                else if (Mathf.Abs(normal.y) < Mathf.Abs(normal.z))
                    tangent = new Vector3(0, 1, 0);
                else
                    tangent = new Vector3(0, 0, 1);

                tangent = Vector3.Cross(tangent, normal);
                tangent = Vector3.Cross(normal, tangent);
                return tangent.normalized;
            }
        }

        // LSCM chart parameterization
        private class LSCMParameterizer : IChartParameterizer
        {
            public bool Parameterize(List<Vector3> positions, List<int> indices, List<Vector2> outTexCoords)
            {
                // This is a simplified implementation - a full LSCM implementation would use
                // least squares minimization to find an angle-preserving parameterization

                // Find two vertices that are farthest apart to pin
                int v0 = 0, v1 = 0;
                float maxDistSq = 0;
                for (int i = 0; i < positions.Count; i++)
                {
                    for (int j = i + 1; j < positions.Count; j++)
                    {
                        float distSq = Vector3.SqrMagnitude(positions[i] - positions[j]);
                        if (distSq > maxDistSq)
                        {
                            maxDistSq = distSq;
                            v0 = i;
                            v1 = j;
                        }
                    }
                }

                // Initial simple solution - in a real implementation this would
                // invoke a proper conformal mapping algorithm
                // For now, we'll use a basic scaling of our orthogonal projection
                OrthoParameterizer ortho = new OrthoParameterizer();
                ortho.Parameterize(positions, indices, outTexCoords);

                // Scale and fit to [0,1] range
                NormalizeTexCoords(outTexCoords);

                return true;
            }

            private void NormalizeTexCoords(List<Vector2> texCoords)
            {
                // Find bounds
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                foreach (Vector2 uv in texCoords)
                {
                    min.x = Mathf.Min(min.x, uv.x);
                    min.y = Mathf.Min(min.y, uv.y);
                    max.x = Mathf.Max(max.x, uv.x);
                    max.y = Mathf.Max(max.y, uv.y);
                }

                // Scale to [0,1]
                Vector2 size = max - min;
                for (int i = 0; i < texCoords.Count; i++)
                {
                    texCoords[i] = new Vector2(
                        (texCoords[i].x - min.x) / size.x,
                        (texCoords[i].y - min.y) / size.y
                    );
                }
            }
        }

        // Chart segmentation
        private class ChartGenerator
        {
            public List<List<int>> GenerateCharts(List<Vector3> positions, List<Vector3> normals, List<int> indices, ChartOptions options)
            {
                List<List<int>> charts = new List<List<int>>();
                int triangleCount = indices.Count / 3;

                // Simple segmentation by normal clustering
                bool[] assigned = new bool[triangleCount];

                for (int i = 0; i < triangleCount; i++)
                {
                    if (assigned[i])
                        continue;

                    // Start a new chart with this triangle
                    List<int> chartFaces = new List<int>();
                    chartFaces.Add(i);
                    assigned[i] = true;

                    // Get the normal of this triangle
                    Vector3 baseNormal = GetFaceNormal(positions, indices, i);

                    // Grow the chart by adding adjacent faces with similar normals
                    GrowChart(positions, normals, indices, assigned, chartFaces, baseNormal, options.normalDeviationWeight);

                    charts.Add(chartFaces);
                }

                return charts;
            }

            private Vector3 GetFaceNormal(List<Vector3> positions, List<int> indices, int faceIndex)
            {
                int i0 = indices[faceIndex * 3];
                int i1 = indices[faceIndex * 3 + 1];
                int i2 = indices[faceIndex * 3 + 2];

                Vector3 v0 = positions[i0];
                Vector3 v1 = positions[i1];
                Vector3 v2 = positions[i2];

                return Vector3.Cross(v1 - v0, v2 - v0).normalized;
            }

            private void GrowChart(List<Vector3> positions, List<Vector3> normals, List<int> indices,
                                 bool[] assigned, List<int> chartFaces, Vector3 baseNormal, float normalThreshold)
            {
                // A queue of triangles to check
                Queue<int> queue = new Queue<int>(chartFaces);

                while (queue.Count > 0)
                {
                    int faceIndex = queue.Dequeue();

                    // Check adjacent triangles
                    for (int i = 0; i < 3; i++)
                    {
                        int edgeStart = indices[faceIndex * 3 + i];
                        int edgeEnd = indices[faceIndex * 3 + (i + 1) % 3];

                        // Find triangle sharing this edge
                        for (int j = 0; j < indices.Count / 3; j++)
                        {
                            if (assigned[j] || j == faceIndex)
                                continue;

                            // Check if this triangle shares the edge
                            bool edgeShared = false;
                            for (int k = 0; k < 3; k++)
                            {
                                int adjacentStart = indices[j * 3 + k];
                                int adjacentEnd = indices[j * 3 + (k + 1) % 3];

                                if ((adjacentStart == edgeEnd && adjacentEnd == edgeStart) ||
                                    (adjacentStart == edgeStart && adjacentEnd == edgeEnd))
                                {
                                    edgeShared = true;
                                    break;
                                }
                            }

                            if (edgeShared)
                            {
                                // Check if normal is similar enough
                                Vector3 normal = GetFaceNormal(positions, indices, j);
                                float dot = Vector3.Dot(normal, baseNormal);

                                if (dot > 1.0f - normalThreshold)
                                {
                                    // Add to chart
                                    chartFaces.Add(j);
                                    assigned[j] = true;
                                    queue.Enqueue(j);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Chart packing
        private class ChartPacker
        {
            private class ChartRect
            {
                public int width;
                public int height;
                public int x;
                public int y;
                public bool rotated;
                public int chartIndex;

                public ChartRect(int w, int h, int index)
                {
                    width = w;
                    height = h;
                    chartIndex = index;
                    x = y = 0;
                    rotated = false;
                }
            }

            public bool PackCharts(List<List<Vector2>> charts, PackOptions options,
                                   out int atlasWidth, out int atlasHeight,
                                   out List<Vector2> chartOffsets, out List<bool> chartRotated)
            {
                // Convert charts to rectangles
                List<ChartRect> rects = new List<ChartRect>();
                for (int i = 0; i < charts.Count; i++)
                {
                    // Find bounds of chart
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                    Vector2 max = new Vector2(float.MinValue, float.MinValue);

                    foreach (Vector2 uv in charts[i])
                    {
                        min.x = Mathf.Min(min.x, uv.x);
                        min.y = Mathf.Min(min.y, uv.y);
                        max.x = Mathf.Max(max.x, uv.x);
                        max.y = Mathf.Max(max.y, uv.y);
                    }

                    // Convert to integer size with padding
                    int width = Mathf.CeilToInt((max.x - min.x) * options.texelsPerUnit) + options.padding * 2;
                    int height = Mathf.CeilToInt((max.y - min.y) * options.texelsPerUnit) + options.padding * 2;

                    rects.Add(new ChartRect(width, height, i));
                }

                // Sort rectangles by height or area for better packing
                rects.Sort((a, b) => (b.height * b.width).CompareTo(a.height * a.width));

                // Simple skyline packing algorithm
                atlasWidth = 0;
                atlasHeight = 0;
                bool success = BinPackRectangles(rects, options, out atlasWidth, out atlasHeight);

                // TODO:
                // if (!success)
                //     return false;

                // Extract chart offsets and rotation flags
                chartOffsets = new List<Vector2>(charts.Count);
                chartRotated = new List<bool>(charts.Count);

                for (int i = 0; i < charts.Count; i++)
                {
                    chartOffsets.Add(Vector2.zero);
                    chartRotated.Add(false);
                }

                foreach (ChartRect rect in rects)
                {
                    chartOffsets[rect.chartIndex] = new Vector2(rect.x + options.padding, rect.y + options.padding);
                    chartRotated[rect.chartIndex] = rect.rotated;
                }

                return true;
            }

            private bool BinPackRectangles(List<ChartRect> rects, PackOptions options, out int atlasWidth, out int atlasHeight)
            {
                // Start with an initial guess at atlas size
                atlasWidth = 256;
                atlasHeight = 256;

                // Keep trying larger sizes until everything fits
                bool allFit = false;

                while (!allFit && atlasWidth <= 8192 && atlasHeight <= 8192)
                {
                    if (TryPackRectangles(rects, atlasWidth, atlasHeight, options.rotateCharts))
                    {
                        allFit = true;
                    }
                    else
                    {
                        // Increase size
                        if (atlasWidth <= atlasHeight)
                            atlasWidth *= 2;
                        else
                            atlasHeight *= 2;
                    }
                }

                return allFit;
            }

            private bool TryPackRectangles(List<ChartRect> rects, int width, int height, bool allowRotation)
            {
                // Reset positions
                foreach (ChartRect rect in rects)
                {
                    rect.x = rect.y = 0;
                    rect.rotated = false;
                }

                // Simple skyline bin packing
                List<int> skyline = new List<int>();
                for (int i = 0; i < width; i++)
                    skyline.Add(0);

                foreach (ChartRect rect in rects)
                {
                    int rectWidth = rect.width;
                    int rectHeight = rect.height;
                    bool rotate = false;

                    // Try to find position with minimum skyline height
                    int bestX = -1;
                    int bestY = height;

                    // Try normal orientation
                    for (int x = 0; x <= width - rectWidth; x++)
                    {
                        int maxSkylineHeight = 0;
                        for (int i = 0; i < rectWidth; i++)
                            maxSkylineHeight = Mathf.Max(maxSkylineHeight, skyline[x + i]);

                        if (maxSkylineHeight + rectHeight <= height && maxSkylineHeight < bestY)
                        {
                            bestX = x;
                            bestY = maxSkylineHeight;
                        }
                    }

                    // Try rotated if allowed
                    if (allowRotation && rectWidth != rectHeight)
                    {
                        for (int x = 0; x <= width - rectHeight; x++)
                        {
                            int maxSkylineHeight = 0;
                            for (int i = 0; i < rectHeight; i++)
                                maxSkylineHeight = Mathf.Max(maxSkylineHeight, skyline[x + i]);

                            if (maxSkylineHeight + rectWidth <= height && maxSkylineHeight < bestY)
                            {
                                bestX = x;
                                bestY = maxSkylineHeight;
                                rotate = true;
                            }
                        }
                    }

                    // If we couldn't find a valid position, return false
                    if (bestX < 0)
                        return false;

                    // Place the rectangle
                    rect.x = bestX;
                    rect.y = bestY;
                    rect.rotated = rotate;

                    // Update skyline
                    int w = rotate ? rectHeight : rectWidth;
                    int h = rotate ? rectWidth : rectHeight;

                    for (int i = 0; i < w; i++)
                        skyline[bestX + i] = bestY + h;
                }

                return true;
            }
        }

        // Helper for building meshes
        private class MeshBuilder
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector3> normals = new List<Vector3>();
            public List<Vector2> uvs = new List<Vector2>();
            public List<int> indices = new List<int>();
            public List<int> materialIndices = new List<int>();
            public List<bool> faceIgnore = new List<bool>();

            // Map faces to original mesh faces when needed
            public List<int> faceMap = new List<int>();

            public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                int index = vertices.Count;
                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv);
                return index;
            }

            public void AddTriangle(int v0, int v1, int v2, int materialIndex = 0, bool ignore = false)
            {
                indices.Add(v0);
                indices.Add(v1);
                indices.Add(v2);
                materialIndices.Add(materialIndex);
                faceIgnore.Add(ignore);
            }
        }
        #endregion

        /// <summary>
        /// Creates a new XAtlas instance.
        /// </summary>
        public XAtlas()
        {
            _context = new Context();
        }

        /// <summary>
        /// Sets the progress callback function.
        /// </summary>
        /// <param name="progressCallback">The progress callback function</param>
        public void SetProgressCallback(ProgressCallback progressCallback)
        {
            _context.progressCallback = progressCallback;
        }

        /// <summary>
        /// Adds a Unity mesh to the atlas generation.
        /// </summary>
        /// <param name="mesh">The Unity mesh to add</param>
        /// <returns>Error code</returns>
        public AddMeshError AddMesh(UnityEngine.Mesh mesh)
        {
            if (mesh == null)
                return AddMeshError.Error;

            try
            {
                _context.inputMeshes.Add(mesh);

                // Create a mesh builder to track the mesh data
                MeshBuilder builder = new MeshBuilder();

                // Copy mesh data
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uvs = mesh.uv;
                int[] indices = mesh.triangles;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = (normals != null && normals.Length > i) ? normals[i] : Vector3.up;
                    Vector2 uv = (uvs != null && uvs.Length > i) ? uvs[i] : Vector2.zero;
                    builder.AddVertex(vertices[i], normal, uv);
                }

                // Process triangles
                for (int i = 0; i < indices.Length; i += 3)
                {
                    builder.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
                    builder.faceMap.Add(i / 3); // Map to original face index
                }

                _context.meshBuilders.Add(builder);

                return AddMeshError.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding mesh: {e.Message}");
                return AddMeshError.Error;
            }
        }

        /// <summary>
        /// Adds a mesh to the atlas using the provided mesh declaration.
        /// </summary>
        /// <param name="meshDecl">The mesh declaration</param>
        /// <returns>Error code</returns>
        public AddMeshError AddMesh(MeshDecl meshDecl)
        {
            if (meshDecl == null || meshDecl.vertexPositionData == null)
                return AddMeshError.Error;

            if (meshDecl.vertexCount == 0)
                return AddMeshError.Error;

            try
            {
                // Create a mesh builder to track the mesh data
                MeshBuilder builder = new MeshBuilder();

                // Copy vertex data
                for (int i = 0; i < meshDecl.vertexCount; i++)
                {
                    Vector3 position = meshDecl.vertexPositionData[i];
                    Vector3 normal = (meshDecl.vertexNormalData != null && i < meshDecl.vertexNormalData.Length)
                        ? meshDecl.vertexNormalData[i] : Vector3.up;
                    Vector2 uv = (meshDecl.vertexUvData != null && i < meshDecl.vertexUvData.Length)
                        ? meshDecl.vertexUvData[i] : Vector2.zero;

                    builder.AddVertex(position, normal, uv);
                }

                // Process indices/triangles
                int indexCount = meshDecl.indexCount;
                if (indexCount == 0)
                {
                    // Generate implicit indices
                    indexCount = meshDecl.vertexCount;
                    for (int i = 0; i < indexCount; i += 3)
                    {
                        if (i + 2 < indexCount)
                        {
                            int materialIndex = (meshDecl.faceMaterialData != null && i/3 < meshDecl.faceMaterialData.Length)
                                ? meshDecl.faceMaterialData[i/3] : 0;
                            bool ignore = (meshDecl.faceIgnoreData != null && i/3 < meshDecl.faceIgnoreData.Length)
                                ? meshDecl.faceIgnoreData[i/3] : false;

                            builder.AddTriangle(i, i + 1, i + 2, materialIndex, ignore);
                            builder.faceMap.Add(i / 3);
                        }
                    }
                }
                else
                {
                    // Use provided indices
                    for (int i = 0; i < indexCount; i += 3)
                    {
                        if (i + 2 < indexCount)
                        {
                            int v0 = meshDecl.indexData[i] + meshDecl.indexOffset;
                            int v1 = meshDecl.indexData[i + 1] + meshDecl.indexOffset;
                            int v2 = meshDecl.indexData[i + 2] + meshDecl.indexOffset;

                            // Check for out of range indices
                            if (v0 >= meshDecl.vertexCount || v1 >= meshDecl.vertexCount || v2 >= meshDecl.vertexCount)
                                return AddMeshError.IndexOutOfRange;

                            int materialIndex = (meshDecl.faceMaterialData != null && i/3 < meshDecl.faceMaterialData.Length)
                                ? meshDecl.faceMaterialData[i/3] : 0;
                            bool ignore = (meshDecl.faceIgnoreData != null && i/3 < meshDecl.faceIgnoreData.Length)
                                ? meshDecl.faceIgnoreData[i/3] : false;

                            builder.AddTriangle(v0, v1, v2, materialIndex, ignore);
                            builder.faceMap.Add(i / 3);
                        }
                    }
                }

                _context.meshBuilders.Add(builder);
                return AddMeshError.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding mesh: {e.Message}");
                return AddMeshError.Error;
            }
        }

        /// <summary>
        /// Adds a UV mesh to the atlas.
        /// </summary>
        /// <param name="uvMeshDecl">The UV mesh declaration</param>
        /// <returns>Error code</returns>
        public AddMeshError AddUvMesh(UvMeshDecl uvMeshDecl)
        {
            if (uvMeshDecl == null || uvMeshDecl.vertexUvData == null)
                return AddMeshError.Error;

            if (uvMeshDecl.vertexCount == 0)
                return AddMeshError.Error;

            try
            {
                // Create a mesh builder
                MeshBuilder builder = new MeshBuilder();

                // Since this is a UV mesh, we'll create dummy 3D positions
                // mapped to the UV coordinates in 3D space (z=0)
                for (int i = 0; i < uvMeshDecl.vertexCount; i++)
                {
                    Vector2 uv = uvMeshDecl.vertexUvData[i];
                    Vector3 position = new Vector3(uv.x, uv.y, 0);
                    builder.AddVertex(position, Vector3.up, uv);
                }

                // Process indices
                int indexCount = uvMeshDecl.indexCount;
                if (indexCount == 0)
                {
                    // Generate implicit indices
                    indexCount = uvMeshDecl.vertexCount;
                    for (int i = 0; i < indexCount; i += 3)
                    {
                        if (i + 2 < indexCount)
                        {
                            int materialIndex = (uvMeshDecl.faceMaterialData != null && i/3 < uvMeshDecl.faceMaterialData.Length)
                                ? uvMeshDecl.faceMaterialData[i/3] : 0;

                            builder.AddTriangle(i, i + 1, i + 2, materialIndex);
                            builder.faceMap.Add(i / 3);
                        }
                    }
                }
                else
                {
                    // Use provided indices
                    for (int i = 0; i < indexCount; i += 3)
                    {
                        if (i + 2 < indexCount)
                        {
                            int v0 = uvMeshDecl.indexData[i] + uvMeshDecl.indexOffset;
                            int v1 = uvMeshDecl.indexData[i + 1] + uvMeshDecl.indexOffset;
                            int v2 = uvMeshDecl.indexData[i + 2] + uvMeshDecl.indexOffset;

                            // Check for out of range indices
                            if (v0 >= uvMeshDecl.vertexCount || v1 >= uvMeshDecl.vertexCount || v2 >= uvMeshDecl.vertexCount)
                                return AddMeshError.IndexOutOfRange;

                            int materialIndex = (uvMeshDecl.faceMaterialData != null && i/3 < uvMeshDecl.faceMaterialData.Length)
                                ? uvMeshDecl.faceMaterialData[i/3] : 0;

                            builder.AddTriangle(v0, v1, v2, materialIndex);
                            builder.faceMap.Add(i / 3);
                        }
                    }
                }

                _context.meshBuilders.Add(builder);
                return AddMeshError.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding UV mesh: {e.Message}");
                return AddMeshError.Error;
            }
        }

        /// <summary>
        /// Computes charts for all added meshes.
        /// </summary>
        /// <param name="options">Chart computation options</param>
        /// <returns>True if successful</returns>
        public async Task<bool> ComputeChartsAsync(ChartOptions options = null)
        {
            if (_context.meshBuilders.Count == 0)
            {
                Debug.LogWarning("No meshes added to atlas. Call AddMesh first.");
                return false;
            }

            // Use default options if none provided
            if (options == null)
                options = new ChartOptions();

            try
            {
                // Reset cancellation token
                _context.cancellationSource = new CancellationTokenSource();

                // Report progress
                ReportProgress(ProgressCategory.ComputeCharts, 0);

                // Process each mesh in parallel
                List<Task<List<List<List<int>>>>> tasks = new List<Task<List<List<List<int>>>>>();
                foreach (MeshBuilder builder in _context.meshBuilders)
                {
                    tasks.Add(Task.Run(() => ComputeChartsForMesh(builder, options)));
                }

                // Wait for all tasks to complete
                List<List<List<int>>> allMeshCharts = new List<List<List<int>>>();
                for (int i = 0; i < tasks.Count; i++)
                {
                    // TODO:
                    //allMeshCharts.Add(await tasks[i]);

                    // Report progress
                    ReportProgress(ProgressCategory.ComputeCharts, (i + 1) * 100 / tasks.Count);

                    if (_context.cancellationSource.Token.IsCancellationRequested)
                        return false;
                }

                // Store chart data on the context for later use
                _context.atlas.chartCount = 0;
                for (int i = 0; i < allMeshCharts.Count; i++)
                {
                    _context.atlas.chartCount += allMeshCharts[i].Count;
                }

                ReportProgress(ProgressCategory.ComputeCharts, 100);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error computing charts: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Computes charts for a mesh.
        /// </summary>
        private List<List<List<int>>> ComputeChartsForMesh(MeshBuilder builder, ChartOptions options)
        {
            // Generate charts using different strategies
            List<List<int>> charts = new List<List<int>>();

            // 1. Try to use input UVs if requested
            if (options.useInputMeshUvs)
            {
                // Group faces that share consistent UV seams
                // This is a simplified version - the real xatlas has a more complex approach
                charts = GenerateChartsFromInputUvs(builder);
            }

            // 2. If no charts were generated from UVs, use normal-based segmentation
            if (charts.Count == 0)
            {
                ChartGenerator chartGen = new ChartGenerator();
                charts = chartGen.GenerateCharts(
                    builder.vertices,
                    builder.normals,
                    builder.indices,
                    options
                );
            }

            // Parameterize each chart
            List<List<List<int>>> parameterizedCharts = new List<List<List<int>>>();
            parameterizedCharts.Add(charts);

            return parameterizedCharts;
        }

        /// <summary>
        /// Generate charts based on existing UVs.
        /// </summary>
        private List<List<int>> GenerateChartsFromInputUvs(MeshBuilder builder)
        {
            // For the sake of simplicity, we'll just create a single chart
            // A full implementation would split the mesh at UV seams
            List<List<int>> charts = new List<List<int>>();
            List<int> singleChart = new List<int>();

            // Add all face indices to the chart
            int faceCount = builder.indices.Count / 3;
            for (int i = 0; i < faceCount; i++)
            {
                if (!builder.faceIgnore[i])
                    singleChart.Add(i);
            }

            if (singleChart.Count > 0)
                charts.Add(singleChart);

            return charts;
        }

        /// <summary>
        /// Packs charts into an atlas.
        /// </summary>
        /// <param name="options">Packing options</param>
        /// <returns>True if successful</returns>
        public async Task<bool> PackChartsAsync(PackOptions options = null)
        {
            if (_context.atlas.chartCount == 0)
            {
                Debug.LogWarning("No charts computed. Call ComputeCharts first.");
                return false;
            }

            // Use default options if none provided
            if (options == null)
                options = new PackOptions();

            try
            {
                // Reset cancellation token
                _context.cancellationSource = new CancellationTokenSource();

                // Report progress
                ReportProgress(ProgressCategory.PackCharts, 0);

                // Estimate texel scale if not specified
                if (options.texelsPerUnit <= 0)
                {
                    options.texelsPerUnit = EstimateTexelsPerUnit(_context.meshBuilders, options.resolution);
                }

                // Pack charts
                bool result = await Task.Run(() => {
                    return PackChartsInternal(options);
                });

                if (!result)
                    return false;

                // Build output meshes
                result = await BuildOutputMeshesAsync();

                ReportProgress(ProgressCategory.PackCharts, 100);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error packing charts: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Internal chart packing implementation.
        /// </summary>
        private bool PackChartsInternal(PackOptions options)
        {
            // This is a simplified packing implementation
            // In a real implementation, we would use a more sophisticated algorithm

            // Create a list of chart UVs for each mesh
            List<List<List<Vector2>>> allMeshCharts = new List<List<List<Vector2>>>();

            // For now, just use the input UVs (if available) or generate simple UVs
            foreach (MeshBuilder builder in _context.meshBuilders)
            {
                List<List<Vector2>> meshCharts = new List<List<Vector2>>();

                // Create a single chart with all UVs
                List<Vector2> chartUvs = new List<Vector2>();
                for (int i = 0; i < builder.uvs.Count; i++)
                {
                    chartUvs.Add(builder.uvs[i]);
                }

                if (chartUvs.Count > 0)
                    meshCharts.Add(chartUvs);

                allMeshCharts.Add(meshCharts);
            }

            // Flatten all charts from all meshes
            List<List<Vector2>> allCharts = new List<List<Vector2>>();
            foreach (var meshCharts in allMeshCharts)
            {
                allCharts.AddRange(meshCharts);
            }

            // Pack charts
            ChartPacker packer = new ChartPacker();
            List<Vector2> chartOffsets;
            List<bool> chartRotated;

            bool success = packer.PackCharts(
                allCharts,
                options,
                out int atlasWidth,
                out int atlasHeight,
                out chartOffsets,
                out chartRotated
            );

            if (!success)
                return false;

            // Update atlas properties
            _context.atlas.width = atlasWidth;
            _context.atlas.height = atlasHeight;
            _context.atlas.texelsPerUnit = options.texelsPerUnit;
            _context.atlas.atlasCount = 1;

            // Create atlas image if requested
            if (options.createImage)
            {
                _context.atlas.image = new Color32[atlasWidth * atlasHeight];
                for (int i = 0; i < _context.atlas.image.Length; i++)
                {
                    _context.atlas.image[i] = new Color32(0, 0, 0, 0);
                }

                // In a real implementation, we would rasterize the charts into the image
            }

            // Calculate utilization
            float totalArea = atlasWidth * atlasHeight;
            float usedArea = 0;

            foreach (var chart in allCharts)
            {
                // Calculate chart area (simplified)
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (Vector2 uv in chart)
                {
                    minX = Mathf.Min(minX, uv.x);
                    minY = Mathf.Min(minY, uv.y);
                    maxX = Mathf.Max(maxX, uv.x);
                    maxY = Mathf.Max(maxY, uv.y);
                }

                usedArea += (maxX - minX) * (maxY - minY) * options.texelsPerUnit * options.texelsPerUnit;
            }

            _context.atlas.utilization = new float[1] { usedArea / totalArea };

            return true;
        }

        /// <summary>
        /// Builds output meshes with atlas UVs.
        /// </summary>
        private async Task<bool> BuildOutputMeshesAsync()
        {
            ReportProgress(ProgressCategory.BuildOutputMeshes, 0);

            // Create output meshes
            _context.atlas.meshCount = _context.meshBuilders.Count;
            _context.atlas.meshes = new Mesh[_context.atlas.meshCount];

            for (int i = 0; i < _context.atlas.meshCount; i++)
            {
                MeshBuilder builder = _context.meshBuilders[i];

                Mesh outputMesh = new Mesh();
                outputMesh.vertexCount = builder.vertices.Count;
                outputMesh.indexCount = builder.indices.Count;
                outputMesh.vertexArray = new Vertex[outputMesh.vertexCount];
                outputMesh.indexArray = new int[outputMesh.indexCount];

                // Copy vertices and indices
                for (int v = 0; v < outputMesh.vertexCount; v++)
                {
                    outputMesh.vertexArray[v] = new Vertex
                    {
                        // In a real implementation, we would map to atlas coordinates
                        uv = builder.uvs[v],
                        atlasIndex = 0,  // Default to first atlas
                        chartIndex = 0,  // Simplified - assign all to first chart
                        xref = v         // Reference to original vertex
                    };
                }

                // Copy indices
                for (int idx = 0; idx < outputMesh.indexCount; idx++)
                {
                    outputMesh.indexArray[idx] = builder.indices[idx];
                }

                // Create a dummy chart
                outputMesh.chartCount = 1;
                outputMesh.chartArray = new Chart[1];
                outputMesh.chartArray[0] = new Chart
                {
                    atlasIndex = 0,
                    faceCount = builder.indices.Count / 3,
                    //faceArray = Enumerable.Range(0, builder.indices.Count / 3).ToArray(),
                    type = ChartType.LSCM,
                    material = 0
                };

                _context.atlas.meshes[i] = outputMesh;

                // Report progress
                ReportProgress(ProgressCategory.BuildOutputMeshes, (i + 1) * 100 / _context.atlas.meshCount);

                if (_context.cancellationSource.Token.IsCancellationRequested)
                    return false;

                // Yield to avoid blocking the main thread too long
                await Task.Yield();
            }

            ReportProgress(ProgressCategory.BuildOutputMeshes, 100);
            return true;
        }

        /// <summary>
        /// Generates an atlas using default options.
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> GenerateAsync()
        {
            return await GenerateAsync(new ChartOptions(), new PackOptions());
        }

        /// <summary>
        /// Generates an atlas using the specified options.
        /// </summary>
        /// <param name="chartOptions">Chart computation options</param>
        /// <param name="packOptions">Chart packing options</param>
        /// <returns>True if successful</returns>
        public async Task<bool> GenerateAsync(ChartOptions chartOptions, PackOptions packOptions)
        {
            bool result = await ComputeChartsAsync(chartOptions);
            if (!result)
                return false;

            return await PackChartsAsync(packOptions);
        }

        /// <summary>
        /// Gets the generated atlas.
        /// </summary>
        /// <returns>The atlas</returns>
        public Atlas GetAtlas()
        {
            return _context.atlas;
        }

        /// <summary>
        /// Estimates the texels per unit based on mesh size and target resolution.
        /// </summary>
        private float EstimateTexelsPerUnit(List<MeshBuilder> meshBuilders, int targetResolution)
        {
            float totalArea = 0.0f;

            foreach (MeshBuilder builder in meshBuilders)
            {
                for (int i = 0; i < builder.indices.Count; i += 3)
                {
                    Vector3 v0 = builder.vertices[builder.indices[i]];
                    Vector3 v1 = builder.vertices[builder.indices[i + 1]];
                    Vector3 v2 = builder.vertices[builder.indices[i + 2]];

                    float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
                    totalArea += area;
                }
            }

            if (totalArea <= 0)
                return 1.0f;

            // If no target resolution is specified, aim for approximately 1024x1024
            if (targetResolution <= 0)
                targetResolution = 1024;

            // Estimate texels per unit: texelCount = area * (texelsPerUnit^2)
            // Assuming 75% atlas utilization
            float targetTexels = targetResolution * targetResolution * 0.75f;
            return Mathf.Sqrt(targetTexels / totalArea);
        }

        /// <summary>
        /// Reports progress using the callback if one is set.
        /// </summary>
        private void ReportProgress(ProgressCategory category, int progress)
        {
            if (_context.progressCallback != null)
            {
                bool shouldContinue = _context.progressCallback(category, progress);
                if (!shouldContinue)
                    _context.cancellationSource.Cancel();
            }
        }

        /// <summary>
        /// Cancels any ongoing operations.
        /// </summary>
        public void Cancel()
        {
            _context.cancellationSource.Cancel();
        }

        /// <summary>
        /// Creates a Unity texture from the atlas.
        /// </summary>
        /// <returns>The texture, or null if the atlas has no image</returns>
        public Texture2D CreateTexture()
        {
            if (_context.atlas.image == null || _context.atlas.width == 0 || _context.atlas.height == 0)
                return null;

            Texture2D texture = new Texture2D(_context.atlas.width, _context.atlas.height, TextureFormat.RGBA32, false);
            texture.SetPixels32(_context.atlas.image);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Get a string description of an error code.
        /// </summary>
        public static string GetErrorString(AddMeshError error)
        {
            switch (error)
            {
                case AddMeshError.Success:
                    return "Success";
                case AddMeshError.Error:
                    return "Unspecified error";
                case AddMeshError.IndexOutOfRange:
                    return "Index out of range";
                case AddMeshError.InvalidFaceVertexCount:
                    return "Invalid face vertex count";
                case AddMeshError.InvalidIndexCount:
                    return "Invalid index count";
                default:
                    return "Unknown error";
            }
        }

        /// <summary>
        /// Creates a Unity mesh from an output mesh.
        /// </summary>
        /// <param name="meshIndex">Index of the output mesh</param>
        /// <returns>A Unity mesh with atlas UVs</returns>
        public UnityEngine.Mesh CreateUnityMesh(int meshIndex)
        {
            if (meshIndex < 0 || meshIndex >= _context.atlas.meshCount)
                return null;

            Mesh outputMesh = _context.atlas.meshes[meshIndex];
            MeshBuilder inputBuilder = _context.meshBuilders[meshIndex];

            UnityEngine.Mesh mesh = new UnityEngine.Mesh();

            // Copy vertices
            Vector3[] vertices = new Vector3[outputMesh.vertexCount];
            Vector3[] normals = new Vector3[outputMesh.vertexCount];
            Vector2[] uvs = new Vector2[outputMesh.vertexCount];

            for (int i = 0; i < outputMesh.vertexCount; i++)
            {
                Vertex v = outputMesh.vertexArray[i];
                int xref = v.xref;

                vertices[i] = inputBuilder.vertices[xref];
                normals[i] = inputBuilder.normals[xref];

                // Scale UV coordinates to fit in atlas space
                uvs[i] = new Vector2(
                    v.uv.x / _context.atlas.width,
                    v.uv.y / _context.atlas.height
                );
            }

            // Copy indices
            int[] indices = new int[outputMesh.indexCount];
            Array.Copy(outputMesh.indexArray, indices, outputMesh.indexCount);

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;

            return mesh;
        }
    }
}
