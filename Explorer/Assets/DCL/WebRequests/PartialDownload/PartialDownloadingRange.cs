namespace DCL.WebRequests
{
    public readonly struct PartialDownloadingRange
    {
        public const int CHUNK_SIZE = 1024 * 1024; // 1MB

        public readonly int RangeStart;
        public readonly int RangeEnd;

        public PartialDownloadingRange(int rangeStart, int rangeEnd)
        {
            this.RangeStart = rangeStart;
            this.RangeEnd = rangeEnd;
        }
    }
}
