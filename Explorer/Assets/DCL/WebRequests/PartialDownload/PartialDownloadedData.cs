namespace DCL.WebRequests
{
    public readonly struct PartialDownloadedData
    {
        public readonly byte[]? DestinationArray;
        public readonly int DownloadedSize;
        public readonly int FullFileSize;

        public PartialDownloadedData(byte[]? destinationArray, int downloadedSize, int fullFileSize)
        {
            this.DestinationArray = destinationArray;
            this.DownloadedSize = downloadedSize;
            this.FullFileSize = fullFileSize;
        }
    }
}
