using System;

namespace DCL.WebRequests.Analytics
{
    /// <summary>
    ///     Part of the web request that is filled only if Analytics is attached <br />
    ///     Contains common data that can be used by all metrics
    /// </summary>
    internal interface IWebRequestAnalytics
    {
        event Action<IWebRequestAnalytics> OnDownloadStarted;

        DateTime CreationTime { get; }

        ulong DownloadedBytes { get; }

        ulong UploadedBytes { get; }
    }
}
