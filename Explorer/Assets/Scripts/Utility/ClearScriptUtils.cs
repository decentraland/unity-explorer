using Microsoft.ClearScript.JavaScript;
using System;
using Unity.Collections;

namespace Utility
{
    public static class ClearScriptUtils
    {
        public static int Write<T>(this ITypedArray<T> clearScriptArray, NativeArray<byte>.ReadOnly source, ulong length, ulong index)
        {
            ulong totalLength = clearScriptArray.Length;

            if (index >= totalLength) { throw new ArgumentOutOfRangeException(nameof(index)); }

            return clearScriptArray.InvokeWithDirectAccess(pData => WriteByteArrayToUnmanagedMemory(source.AsReadOnlySpan(), (int)Math.Min(length, totalLength - index), clearScriptArray.GetPtrWithIndex(pData, index)));
        }

        public static int Write<T>(this ITypedArray<T> clearScriptArray, Memory<byte> source, ulong length, ulong index)
        {
            ulong totalLength = clearScriptArray.Length;

            if (index >= totalLength) { throw new ArgumentOutOfRangeException(nameof(index)); }

            return clearScriptArray.InvokeWithDirectAccess(pData => WriteByteArrayToUnmanagedMemory((ReadOnlySpan<byte>)source.Span, (int)Math.Min(length, totalLength - index), clearScriptArray.GetPtrWithIndex(pData, index)));
        }

        private static IntPtr GetPtrWithIndex<T>(this ITypedArray<T> clearScriptArray, IntPtr pData, ulong index)
        {
            var baseAddr = unchecked((ulong)pData.ToInt64());
            return new IntPtr(unchecked((long)checked(baseAddr + (index * (clearScriptArray.Size / clearScriptArray.Length)))));
        }

        private static unsafe int WriteByteArrayToUnmanagedMemory<T>(ReadOnlySpan<T> sourceArray, int length, IntPtr pDestination) where T: unmanaged
        {
            int sourceLength = sourceArray.Length;
            length = Math.Min(length, sourceLength);

            var targetSpan = new Span<T>(pDestination.ToPointer(), length);
            sourceArray.CopyTo(targetSpan);

            return length;
        }
    }
}
