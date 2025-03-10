using DCL.Optimization.Memory;
using DCL.Utilities.Extensions;
using System;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public readonly int FullFileSize;
        private MemoryChain? memoryOwner;

        public PartialLoadingState(int fullFileSize, bool isFileFullyDownloaded = false)
        {
            memoryOwner = new MemoryChain(ISlabAllocator.SHARED);
            NextRangeStart = 0;
            FullFileSize = fullFileSize;
            IsFileFullyDownloaded = isFileFullyDownloaded;
        }

        public PartialLoadingState(in PartialLoadingState otherInstance, bool isFileFullyDownloaded = false) : this(otherInstance.FullFileSize, isFileFullyDownloaded)
        {
            if (otherInstance.memoryOwner != null)
                memoryOwner!.AppendData(otherInstance.memoryOwner);
        }

        public int NextRangeStart { get; private set; }

        public bool IsFileFullyDownloaded { get; private set; }

        private readonly bool IsFullyLoaded() =>
            NextRangeStart >= FullFileSize;

        internal void AppendData(ReadOnlySpan<byte> data)
        {
            memoryOwner.EnsureNotNull().AppendData(data);
            NextRangeStart += data.Length;
            IsFileFullyDownloaded = IsFullyLoaded();
        }

        internal readonly MemoryChain PeekMemory() =>
            memoryOwner.EnsureNotNull();

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal MemoryChain TransferMemoryOwnership()
        {
            if (memoryOwner == null)
                throw new InvalidOperationException("Memory owner is null");

            var memoryOwnerToReturn = memoryOwner;
            memoryOwner = null;
            return memoryOwnerToReturn;
        }

        public readonly PartialLoadingState DeepCopy() =>
            new (this, IsFileFullyDownloaded);

        public readonly ChainMemoryIterator AsIterator() =>
            memoryOwner.EnsureNotNull().AsMemoryIterator();

        public readonly void Dispose()
        {
            memoryOwner?.Dispose();
        }
    }
}
