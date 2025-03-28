using System;

namespace DCL.WebRequests
{
    public partial interface IWebRequest
    {
        DateTime CreationTime { get; }

        ulong DownloadedBytes { get; }

        ulong UploadedBytes { get; }
    }
}
