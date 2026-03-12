using System;
using Unity.Collections;

namespace Utility
{
    public static class TypedArrayUtils
    {
        public static void Write<T>(this IDCLTypedArray<T> array,
            NativeArray<byte>.ReadOnly source, ulong length, ulong index) where T : unmanaged
        {
            ulong totalLength = array.Length;

            if (index >= totalLength) { throw new ArgumentOutOfRangeException(nameof(index)); }

#if UNITY_WEBGL
            WriteBytesManaged(array, source.AsReadOnlySpan(), length, index, totalLength);
#else
            array.InvokeWithDirectAccess(pData => WriteByteArrayToUnmanagedMemory(source.AsReadOnlySpan(), (int)Math.Min(length, totalLength - index), array.GetPtrWithIndex(pData, index)));
#endif
        }

#if !UNITY_WEBGL

        private static IntPtr GetPtrWithIndex<T>(this IDCLTypedArray<T> array, IntPtr pData,
            ulong index) where T : unmanaged
        {
            var baseAddress = unchecked((ulong)pData.ToInt64());
            return new IntPtr(unchecked((long)checked(baseAddress + (index * (array.Size / array.Length)))));
        }

        private static unsafe void WriteByteArrayToUnmanagedMemory<T>(ReadOnlySpan<T> span, int length, IntPtr pDestination) where T : unmanaged
        {
            length = Math.Min(length, span.Length);
            span.CopyTo(new Span<T>(pDestination.ToPointer(), length));
        }
#endif

#if UNITY_WEBGL
        /// <summary>
        /// WebGL path for writing a span of bytes into a typed array at a given element index.
        /// <para>
        /// On WebGL, <see cref="IDCLTypedArray{T}.InvokeWithDirectAccess"/> cannot be used because the
        /// runtime has no direct unmanaged memory access from the managed side. Instead, the target
        /// byte offset is computed from <paramref name="index"/> and the array's bytes-per-element ratio,
        /// and the data is handed off to <see cref="IDCLTypedArray{T}.WriteBytes"/> which marshals it
        /// through managed memory into the underlying JS typed array.
        /// </para>
        /// </summary>
        private static void WriteBytesManaged<T>(IDCLTypedArray<T> array, ReadOnlySpan<byte> srcSpan,
            ulong length, ulong index, ulong totalLength) where T : unmanaged
        {
            ulong bytesPerElement = array.Size / totalLength;
            ulong byteOffset = index * bytesPerElement;
            int maxBytes = (int)Math.Min(length, totalLength - index) * (int)bytesPerElement;
            int bytesToCopy = Math.Min(maxBytes, srcSpan.Length);
            array.WriteBytes(srcSpan[..bytesToCopy].ToArray(), 0, (ulong)bytesToCopy, byteOffset);
        }
#endif
    }
}
