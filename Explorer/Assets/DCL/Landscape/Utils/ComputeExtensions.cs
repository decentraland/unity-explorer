using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Landscape.Utils
{
    public static class ComputeShaderExtensions
    {
        [ThreadStatic] private static int[]? _intsScratch;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int[] CreateScratchBuffer(int needed)
        {
            var b = _intsScratch;
            if (b == null || b.Length < needed)
                _intsScratch = b = new int[Mathf.NextPowerOfTwo(needed)];

            return b;
        }

        public static void SetInt2(this ComputeShader cs, int nameId, int x, int y)
        {
            int[] b = CreateScratchBuffer(2);
            b[0] = x;
            b[1] = y;
            cs.SetInts(nameId, b);
        }
    }
}
