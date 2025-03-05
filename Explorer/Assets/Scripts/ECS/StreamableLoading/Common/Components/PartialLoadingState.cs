using DCL.Optimization.Memory;
using System;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public readonly int FullFileSize;
        private MemoryChain memoryOwner;

        public PartialLoadingState(int fullFileSize, bool isFileFullyDownloaded = false)
        {
            memoryOwner = new MemoryChain(ISlabAllocator.SHARED);
            NextRangeStart = 0;
            FullFileSize = fullFileSize;
            IsFileFullyDownloaded = isFileFullyDownloaded;
        }

        public PartialLoadingState(in PartialLoadingState otherInstance, bool isFileFullyDownloaded = false) : this(otherInstance.FullFileSize, isFileFullyDownloaded)
        {
            memoryOwner.AppendData(otherInstance.memoryOwner);
        }

        public int NextRangeStart { get; private set; }

        public bool IsFileFullyDownloaded;
        public readonly bool FullyDownloaded => NextRangeStart >= FullFileSize;

        internal void AppendData(ReadOnlyMemory<byte> data)
        {
            memoryOwner.AppendData(data.Span);
            NextRangeStart += data.Length;
            IsFileFullyDownloaded = FullyDownloaded;
        }

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal MemoryChain TransferMemoryOwnership()
        {
            var memoryOwnerToReturn = memoryOwner;
            memoryOwner = MemoryChain.EMPTY;
            return memoryOwnerToReturn;
        }

        public readonly ChainMemoryIterator AsIterator() =>
            memoryOwner.AsMemoryIterator();

        public void Dispose()
        {
            memoryOwner.Dispose();
        }
    }
}
