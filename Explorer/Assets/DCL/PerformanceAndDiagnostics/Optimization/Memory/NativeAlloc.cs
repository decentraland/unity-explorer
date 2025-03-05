using System;
using System.Runtime.InteropServices;

namespace DCL.Optimization.Memory
{
    /// <summary>
    /// I don't have a clue why I'v been getting random crashes the whole day using UnsafeUtility.Malloc.
    /// Here we go, plain old good C malloc.
    /// </summary>
    public static class NativeAlloc
    {
        [DllImport("libc", EntryPoint = "malloc", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Malloc(nuint size);

        [DllImport("libc", EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Free(IntPtr ptr);
    }
}
