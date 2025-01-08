namespace DCL.WebRequests.PartialDownload
{
    public struct FullDownloadedData
    {
        public readonly byte[] DataBuffer;

        public FullDownloadedData(byte[] dataBuffer)
        {
            DataBuffer = dataBuffer;
        }
    }
}
