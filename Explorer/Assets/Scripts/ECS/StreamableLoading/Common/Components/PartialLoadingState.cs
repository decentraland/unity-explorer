using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Buffers;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        private static readonly ArrayPool<byte> FULL_FILE_POOL = ArrayPool<byte>.Create(64 * 1024 * 1024, 10); // 64 MB
        private static readonly MemoryOwner<byte> EMPTY_MEMORY_OWNER = MemoryOwner<byte>.Empty;

        public readonly Memory<byte> FullData => memoryOwner.Memory;
        public readonly int FullFileSize => memoryOwner.Length;

        public int NextRangeStart;

        // Add expiration time/TTL/additional data here as required

        private MemoryOwner<byte> memoryOwner;

        public PartialLoadingState(int fullFileSize)
        {
            memoryOwner = MemoryOwner<byte>.Allocate(fullFileSize, FULL_FILE_POOL);
            NextRangeStart = 0;
        }

        public PartialLoadingState(in PartialLoadingState otherInstance) : this(otherInstance.FullFileSize)
        {
            otherInstance.FullData.CopyTo(FullData);
        }

        public bool FullyDownloaded => NextRangeStart >= FullFileSize;

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
