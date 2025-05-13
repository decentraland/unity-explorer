using System.Buffers;
using UnityEngine;

namespace Utility.Primitives
{
    /// <summary>
    ///     Pools dedicated to manipulate with vertices, normals, uvs and triangles of primitives on the main thread
    /// </summary>
    internal static class PrimitivesBuffersPool
    {
        private const int MAX_ARRAY_LENGTH = 1024;

        // Only 1 array is used concurrently (creation from the main thread only)
        private const int MAX_ARRAYS_PER_BUCKET = 1;

        // Vertices + Normals
        public static readonly ArrayPool<Vector3> EQUAL_TO_VERTICES = ArrayPool<Vector3>.Create(MAX_ARRAY_LENGTH, MAX_ARRAYS_PER_BUCKET * 2);
        public static readonly ArrayPool<Vector2> UVS = ArrayPool<Vector2>.Create(MAX_ARRAY_LENGTH, MAX_ARRAYS_PER_BUCKET);
        public static readonly ArrayPool<int> TRIANGLES = ArrayPool<int>.Create(MAX_ARRAY_LENGTH * 10, MAX_ARRAYS_PER_BUCKET);
    }
}
