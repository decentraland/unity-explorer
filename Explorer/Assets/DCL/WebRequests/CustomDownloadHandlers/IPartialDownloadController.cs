using System;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public interface IPartialDownloadController
    {
        public event Action<float> OnDownloadProgress;

        public event Action OnDownloadCompleted;

        public event Action OnDownloadError;

        void StartDownload();

        void StopDownload();
    }
}
