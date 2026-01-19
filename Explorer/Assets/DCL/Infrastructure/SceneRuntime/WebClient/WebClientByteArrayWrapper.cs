using System;
using Utility;

namespace SceneRuntime.WebClient
{
    /// <summary>
    /// A simple wrapper around a byte array that implements IDCLTypedArray byte.
    /// Used when byte arrays are passed from JavaScript via JSON serialization.
    /// </summary>
    public class WebClientByteArrayWrapper : IDCLTypedArray<byte>
    {
        private readonly byte[] data;

        public WebClientByteArrayWrapper(byte[] data)
        {
            this.data = data ?? Array.Empty<byte>();
        }

        public ulong Length => (ulong)data.Length;

        public ulong Size => (ulong)data.Length;

        public IDCLArrayBuffer ArrayBuffer => new SimpleArrayBuffer(data);

        public ulong Read(ulong index, ulong length, byte[] destination, ulong destinationIndex)
        {
            if (length == 0) return 0;

            ulong actualLength = Math.Min(length, (ulong)destination.LongLength - destinationIndex);
            actualLength = Math.Min(actualLength, (ulong)data.Length - index);

            Array.Copy(data, (long)index, destination, (long)destinationIndex, (long)actualLength);
            return actualLength;
        }

        public void ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
        {
            if (count == 0) return;

            ulong actualCount = Math.Min(count, (ulong)destination.LongLength - destinationIndex);
            actualCount = Math.Min(actualCount, (ulong)data.Length - offset);

            Array.Copy(data, (long)offset, destination, (long)destinationIndex, (long)actualCount);
        }

        public void WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
        {
            if (count == 0) return;

            ulong actualCount = Math.Min(count, (ulong)source.LongLength - sourceIndex);
            actualCount = Math.Min(actualCount, (ulong)data.Length - offset);

            Array.Copy(source, (long)sourceIndex, data, (long)offset, (long)actualCount);
        }

        public void InvokeWithDirectAccess(Action<IntPtr> action) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        public int InvokeWithDirectAccess(Func<IntPtr, int> func) =>
            throw new NotSupportedException("WebGL does not support direct memory access");

        /// <summary>
        /// Simple array buffer implementation wrapping a byte array.
        /// </summary>
        private class SimpleArrayBuffer : IDCLArrayBuffer
        {
            private readonly byte[] data;

            public SimpleArrayBuffer(byte[] data)
            {
                this.data = data;
            }

            public ulong Size => (ulong)data.Length;

            public ulong ReadBytes(ulong offset, ulong count, byte[] destination, ulong destinationIndex)
            {
                if (count == 0) return 0;

                ulong actualCount = Math.Min(count, (ulong)destination.LongLength - destinationIndex);
                actualCount = Math.Min(actualCount, (ulong)data.Length - offset);

                Array.Copy(data, (long)offset, destination, (long)destinationIndex, (long)actualCount);
                return actualCount;
            }

            public ulong WriteBytes(byte[] source, ulong sourceIndex, ulong count, ulong offset)
            {
                if (count == 0) return 0;

                ulong actualCount = Math.Min(count, (ulong)source.LongLength - sourceIndex);
                actualCount = Math.Min(actualCount, (ulong)data.Length - offset);

                Array.Copy(source, (long)sourceIndex, data, (long)offset, (long)actualCount);
                return actualCount;
            }

            public void InvokeWithDirectAccess(Action<IntPtr> action) =>
                throw new NotSupportedException("WebGL does not support direct memory access");

            public TResult InvokeWithDirectAccess<TResult>(Func<IntPtr, TResult> func) =>
                throw new NotSupportedException("WebGL does not support direct memory access");
        }
    }
}
