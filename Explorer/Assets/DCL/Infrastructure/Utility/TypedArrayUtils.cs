using System;
using Unity.Collections;

namespace Utility
{
    public static class TypedArrayUtils
    {
        public static int Write<T>(this IDCLTypedArray<T> array,
            NativeArray<byte>.ReadOnly source, ulong length, ulong index) where T : unmanaged
        {
            ulong totalLength = array.Length;

            if (index >= totalLength) { throw new ArgumentOutOfRangeException(nameof(index)); }

#if UNITY_WEBGL
            ulong bytesPerElement = array.Size / totalLength;
            ulong byteOffset = index * bytesPerElement;
            int maxBytes = (int)Math.Min(length, totalLength - index) * (int)bytesPerElement;
            ReadOnlySpan<byte> srcSpan = source.AsReadOnlySpan();
            int bytesToCopy = Math.Min(maxBytes, srcSpan.Length);
            byte[] buffer = srcSpan.Slice(0, bytesToCopy).ToArray();
            array.WriteBytes(buffer, 0, (ulong)bytesToCopy, byteOffset);
            return bytesToCopy;
#else
            return array.InvokeWithDirectAccess(pData => WriteByteArrayToUnmanagedMemory(source.AsReadOnlySpan(), (int)Math.Min(length, totalLength - index), array.GetPtrWithIndex(pData, index)));
#endif
        }

        public static int Write<T>(this IDCLTypedArray<T> array, ReadOnlyMemory<byte> source,
            ulong length, ulong index) where T : unmanaged
        {
            ulong totalLength = array.Length;

            if (index >= totalLength) { throw new ArgumentOutOfRangeException(nameof(index)); }

#if UNITY_WEBGL
            ulong bytesPerElement = array.Size / totalLength;
            ulong byteOffset = index * bytesPerElement;
            int maxBytes = (int)Math.Min(length, totalLength - index) * (int)bytesPerElement;
            ReadOnlySpan<byte> srcSpan = source.Span;
            int bytesToCopy = Math.Min(maxBytes, srcSpan.Length);
            byte[] buffer = srcSpan.Slice(0, bytesToCopy).ToArray();
            array.WriteBytes(buffer, 0, (ulong)bytesToCopy, byteOffset);
            return bytesToCopy;
#else
            return array.InvokeWithDirectAccess(pData => WriteByteArrayToUnmanagedMemory(source.Span, (int)Math.Min(length, totalLength - index), array.GetPtrWithIndex(pData, index)));
#endif
        }

        private static IntPtr GetPtrWithIndex<T>(this IDCLTypedArray<T> array, IntPtr pData,
            ulong index) where T : unmanaged
        {
            var baseAddress = unchecked((ulong)pData.ToInt64());
            return new IntPtr(unchecked((long)checked(baseAddress + (index * (array.Size / array.Length)))));
        }

        private static unsafe int WriteByteArrayToUnmanagedMemory<T>(ReadOnlySpan<T> span, int length, IntPtr pDestination) where T: unmanaged
        {
            int sourceLength = span.Length;
            length = Math.Min(length, sourceLength);

            var targetSpan = new Span<T>(pDestination.ToPointer(), length);
            span.CopyTo(targetSpan);

            return length;
        }
    }
}
