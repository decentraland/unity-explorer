using System;

namespace DCL.WebRequests.PartialDownload
{
    public struct PartialDownloadingData
    {
        public const int CHUNK_SIZE = 1024 * 1024; // 1MB

        public readonly byte[] DataBuffer;

        public int RangeStart;
        public int RangeEnd;
        public int FullFileSize;

        public PartialDownloadingData(byte[] dataBuffer, int rangeStart, int rangeEnd, int fullFileSize = 0)
        {
            this.DataBuffer = dataBuffer;
            this.RangeStart = rangeStart;
            this.RangeEnd = rangeEnd;
            FullFileSize = fullFileSize;
        }

        public void ClearBuffer()
        {
            Array.Clear(DataBuffer, 0, DataBuffer.Length);
        }
    }
}
