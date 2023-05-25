using System.Collections.Generic;
using UnityEngine;

namespace Utility.Primitives
{
    public class PrimitivesUtility
    {
        public static Vector2[] FloatArrayToV2List(IList<float> uvs)
        {
            var uvsResult = new Vector2[uvs.Count / 2];
            var uvsResultIndex = 0;

            for (var i = 0; i < uvs.Count;) { uvsResult[uvsResultIndex++] = new Vector2(uvs[i++], uvs[i++]); }

            return uvsResult;
        }
    }
}
