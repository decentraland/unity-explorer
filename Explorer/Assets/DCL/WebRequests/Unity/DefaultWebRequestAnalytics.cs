using DCL.WebRequests.Analytics;
using System;
using UnityEngine.Networking;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Reused for every attempt made
    /// </summary>
    internal class DefaultWebRequestAnalytics : IWebRequestAnalytics
    {
        public event Action<IWebRequestAnalytics>? OnDownloadStarted;

        private UnityWebRequest? unityWebRequest;

        private bool downloadStarted;

        public void OnStarted(UnityWebRequest unityWebRequest)
        {
            this.unityWebRequest = unityWebRequest;
            CreationTime = DateTime.Now;
            downloadStarted = false;
        }

        public DateTime CreationTime { get; private set; }
        public ulong DownloadedBytes => unityWebRequest!.downloadedBytes;
        public ulong UploadedBytes => unityWebRequest!.uploadedBytes;

        public void Update()
        {
            if (DownloadedBytes > 0 && !downloadStarted)
            {
                OnDownloadStarted?.Invoke(this);
                downloadStarted = true;
            }
        }
    }
}
