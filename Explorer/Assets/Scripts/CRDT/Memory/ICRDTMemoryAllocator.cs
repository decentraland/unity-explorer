using System;
using System.Buffers;

namespace CRDT.Memory
{
    /// <summary>
    ///     The abstraction controls how byte arrays for CRDT Messages are retrieved and released.
    /// </summary>
    public interface ICRDTMemoryAllocator : IDisposable
    {
        IMemoryOwner<byte> GetMemoryBuffer(in ReadOnlyMemory<byte> originalStream, int shift, int length);
    }

    public static class CRDTMemoryAllocatorExtensions
    {
        public static IMemoryOwner<byte> GetMemoryBuffer(this ICRDTMemoryAllocator allocator, int length) =>
            allocator.GetMemoryBuffer(ReadOnlyMemory<byte>.Empty, 0, length);

        public static IMemoryOwner<byte> GetMemoryBuffer(this ICRDTMemoryAllocator allocator, in ReadOnlyMemory<byte> originalStream) =>
            allocator.GetMemoryBuffer(originalStream, 0, originalStream.Length);
    }
}
