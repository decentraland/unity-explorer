using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DCL.Optimization.Memory
{
    /// <summary>
    /// I don't have a clue why I'v been getting random crashes the whole day using UnsafeUtility.Malloc.
    /// Here we go, plain old good C malloc.
    /// </summary>
    public static class NativeAlloc
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        private const string LIBRARY = "msvcrt.dll";
#else
        private const string LIBRARY = "libc";
#endif


        [DllImport(LIBRARY, EntryPoint = "malloc", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Malloc(nuint size);

        [DllImport(LIBRARY, EntryPoint = "free", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Free(IntPtr ptr);
    }
}
