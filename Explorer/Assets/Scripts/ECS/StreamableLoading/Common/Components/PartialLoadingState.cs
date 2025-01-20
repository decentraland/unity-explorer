using System;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public byte[] FullData { get; }
        public readonly int FullFileSize;

        // TODO assign properly
        public int NextRangeStart;

        // Add expiration time/TTL/additional data here as required

        public bool FullyDownloaded => NextRangeStart >= FullFileSize;

        public PartialLoadingState(byte[] fullData, int fullFileSize)
        {
            FullData = fullData;
            FullFileSize = fullFileSize;
            NextRangeStart = 0;
        }

        public void Dispose()
        {
            // TODO release data to the pool
        }
    }
}
