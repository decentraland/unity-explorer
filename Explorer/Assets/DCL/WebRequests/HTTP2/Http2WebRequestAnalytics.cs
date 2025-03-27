using Best.HTTP;
using Best.HTTP.Response;
using DCL.WebRequests.Analytics;
using System;

namespace DCL.WebRequests.HTTP2
{
    internal class Http2WebRequestAnalytics : IWebRequestAnalytics
    {
        private readonly HTTPRequest request;

        public DateTime CreationTime { get; }

        public ulong DownloadedBytes { get; private set; }

        public ulong UploadedBytes { get; private set; }

        public event Action<IWebRequestAnalytics>? OnDownloadStarted;

        public Http2WebRequestAnalytics(HTTPRequest request)
        {
            this.request = request;
            CreationTime = DateTime.Now;
            this.request.DownloadSettings.OnDownloadStarted += OnStarted;
            this.request.DownloadSettings.OnDownloadProgress += OnDownloadProgress;
            this.request.UploadSettings.OnUploadProgress += OnUploadProgress;
        }

        private void OnDownloadProgress(HTTPRequest req, long progress, long length)
        {
            DownloadedBytes = (ulong)progress;
        }

        private void OnUploadProgress(HTTPRequest req, long progress, long length)
        {
            UploadedBytes = (ulong)progress;
        }

        private void OnStarted(HTTPRequest req, HTTPResponse resp, DownloadContentStream stream)
        {
            OnDownloadStarted?.Invoke(this);
        }
    }
}
