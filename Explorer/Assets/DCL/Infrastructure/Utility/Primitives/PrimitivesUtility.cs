using System.Collections.Generic;
using UnityEngine;

namespace Utility.Primitives
{
    public static class PrimitivesUtility
    {
        public static Vector2[] FloatArrayToV2List(IList<float> uvs, Vector2[] uvsResult)
        {
            var uvsResultIndex = 0;

            for (var i = 0; i < uvs.Count && uvsResultIndex < uvsResult.Length;)
                uvsResult[uvsResultIndex++] = new Vector2(uvs[i++], uvs[i++]);

            return uvsResult;
        }

        // Writes UV channel 0: the scene-provided UVs when present, otherwise the primitive's default set
        public static void ApplyUVs(Mesh mesh, IList<float>? customUVs, Vector2[] defaultUVs, int verticesNum)
        {
            if (customUVs is { Count: > 0 })
                mesh.SetUVs(0, FloatArrayToV2List(customUVs, mesh.uv), 0, verticesNum);
            else
                mesh.SetUVs(0, defaultUVs, 0, verticesNum);
        }
    }
}
