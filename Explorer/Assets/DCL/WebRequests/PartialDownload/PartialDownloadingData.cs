using System;

namespace DCL.WebRequests.PartialDownload
{
    public class PartialDownloadingData
    {
        public const int CHUNK_SIZE = 1024 * 1024; // 1MB

        public byte[]? DataBuffer;

        public int RangeStart;
        public int RangeEnd;
        public int FullFileSize;

        public PartialDownloadingData(int rangeStart, int rangeEnd, int fullFileSize = 0)
        {
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
