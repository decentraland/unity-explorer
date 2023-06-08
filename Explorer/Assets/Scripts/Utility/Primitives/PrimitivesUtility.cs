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
    }
}
