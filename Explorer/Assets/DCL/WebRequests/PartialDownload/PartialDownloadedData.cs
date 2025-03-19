using System.IO;

namespace DCL.WebRequests
{
    public readonly struct PartialDownloadedData
    {
        // public readonly byte[]? DestinationArray;
        // public readonly int DownloadedSize;
        // public readonly int FullFileSize;
        //
        // public PartialDownloadedData(byte[]? destinationArray, int downloadedSize, int fullFileSize)
        // {
        //     this.DestinationArray = destinationArray;
        //     this.DownloadedSize = downloadedSize;
        //     this.FullFileSize = fullFileSize;
        // }

        /// <summary>
        ///     The final stream is assigned only if the download is fully completed.
        /// </summary>
        public readonly Stream? FullFileStream;

        public readonly int DownloadedSize;
        public bool IsDownloadComplete => FullFileStream != null;

        public PartialDownloadedData(Stream? fullFileStream, int downloadedSize)
        {
            FullFileStream = fullFileStream;
            DownloadedSize = downloadedSize;
        }
    }
}
