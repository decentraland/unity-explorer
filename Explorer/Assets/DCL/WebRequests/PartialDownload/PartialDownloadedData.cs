using System;
using System.Buffers;

namespace DCL.WebRequests.PartialDownload
{
    public readonly struct PartialDownloadedData : IDisposable
    {
        public readonly byte[]? DestinationArray;
        public readonly int DownloadedSize;
        public readonly int FullFileSize;
        private readonly ArrayPool<byte> pool;

        public PartialDownloadedData(byte[]? destinationArray, int downloadedSize, int fullFileSize, ArrayPool<byte> pool)
        {
            this.DestinationArray = destinationArray;
            this.DownloadedSize = downloadedSize;
            this.FullFileSize = fullFileSize;
            this.pool = pool;
        }

        public void Dispose()
        {
            if (DestinationArray != null)
                pool.Return(DestinationArray);
        }
    }
}
