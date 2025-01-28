using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Buffers;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        private static readonly ArrayPool<byte> FULL_FILE_POOL = ArrayPool<byte>.Create(64 * 1024 * 1024, 10); // 64 MB
        private static readonly MemoryOwner<byte> EMPTY_MEMORY_OWNER = MemoryOwner<byte>.Empty;

        public readonly int FullFileSize;

        // Add expiration time/TTL/additional data here as required

        private MemoryOwner<byte> memoryOwner;

        public PartialLoadingState(int fullFileSize)
        {
            memoryOwner = MemoryOwner<byte>.Allocate(fullFileSize, FULL_FILE_POOL);
            NextRangeStart = 0;
            FullFileSize = fullFileSize;
        }

        public PartialLoadingState(in PartialLoadingState otherInstance) : this(otherInstance.FullFileSize)
        {
            AppendData(otherInstance.FullData[..otherInstance.NextRangeStart]);
        }

        public int NextRangeStart { get; private set; }
        public readonly bool FullyDownloaded => NextRangeStart >= FullFileSize;
        public readonly ReadOnlyMemory<byte> FullData => memoryOwner.Memory;

        internal void AppendData(ReadOnlyMemory<byte> data)
        {
            data.CopyTo(memoryOwner.Memory[NextRangeStart..]);
            NextRangeStart += data.Length;
        }

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal IMemoryOwner<byte> TransferMemoryOwnership()
        {
            MemoryOwner<byte> memoryOwnerToReturn = memoryOwner;
            memoryOwner = EMPTY_MEMORY_OWNER;
            return memoryOwnerToReturn;
        }

        public void Dispose()
        {
            memoryOwner.Dispose();
        }
    }
}
