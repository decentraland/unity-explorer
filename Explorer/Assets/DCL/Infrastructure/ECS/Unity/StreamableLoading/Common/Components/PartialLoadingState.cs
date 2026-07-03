using ECS.StreamableLoading.Cache.Disk;
using System;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public readonly int FullFileSize;
        private SlicedOwnedMemory<byte> memoryOwner;

        public PartialLoadingState(int fullFileSize, bool isFileFullyDownloaded = false)
        {
            memoryOwner = new SlicedOwnedMemory<byte>(fullFileSize);
            NextRangeStart = 0;
            FullFileSize = fullFileSize;
            IsFileFullyDownloaded = isFileFullyDownloaded;
        }

        public PartialLoadingState(in PartialLoadingState otherInstance, bool isFileFullyDownloaded = false) : this(otherInstance.FullFileSize, isFileFullyDownloaded)
        {
            AppendData(otherInstance.FullData[..otherInstance.NextRangeStart]);
        }

        public int NextRangeStart { get; private set; }

        public bool IsFileFullyDownloaded;
        public readonly bool FullyDownloaded => NextRangeStart >= FullFileSize;
        public readonly ReadOnlyMemory<byte> FullData => memoryOwner.Memory;

        internal void AppendData(ReadOnlyMemory<byte> data)
        {
            data.CopyTo(memoryOwner.Memory[NextRangeStart..]);
            NextRangeStart += data.Length;
            IsFileFullyDownloaded = FullyDownloaded;
        }

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal SlicedOwnedMemory<byte> TransferMemoryOwnership()
        {
            var memoryOwnerToReturn = memoryOwner;
            memoryOwner = SlicedOwnedMemory<byte>.EMPTY;
            return memoryOwnerToReturn;
        }

        public void Dispose()
        {
            memoryOwner.Dispose();
        }
    }
}
