namespace DCL.WebRequests.PartialDownload
{
    //Consider making disposable
    public struct FullDownloadedData
    {
        //Use memory or Stream
        public readonly byte[] DataBuffer;

        public FullDownloadedData(byte[] dataBuffer)
        {
            DataBuffer = dataBuffer;
        }
    }
}
