using System;
using UnityEngine.Networking;

namespace DCL.WebRequests.CustomDownloadHandlers
{
    public class PartialDownloadController : IPartialDownloadController
    {
        private const string CONTENT_RANGE_HEADER = "Content-Range";
        private readonly byte[] preallocatedBuffer = new byte[1024*1024];

        public event Action<float>? OnDownloadProgress;
        public event Action? OnDownloadCompleted;
        public event Action? OnDownloadError;

        private readonly string url;
        private readonly string filePath;
        private bool isDownloading;

        public PartialDownloadController(string url, string filePath)
        {
            this.url = url;
            this.filePath = filePath;
        }

        public void StartDownload()
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            var downloadHandler = new PartialDownloadHandlerScript(request, filePath, OnComplete, preallocatedBuffer);
            request.downloadHandler = downloadHandler;
            downloadHandler.StartDownload();
            isDownloading = true;
        }

        public void StopDownload()
        {
            isDownloading = false;
        }

        private void OnComplete(UnityWebRequest request, long downloadedSize)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                if(DownloadHandlersUtils.TryGetFullSize(request.GetResponseHeader(CONTENT_RANGE_HEADER), out long fullSize))
                {
                    OnDownloadProgress?.Invoke((float)downloadedSize / fullSize);
                    if (downloadedSize < fullSize && isDownloading)
                        StartDownload();
                    else
                        isDownloading = false;
                }
                else
                {
                    isDownloading = false;
                    OnDownloadCompleted?.Invoke();
                }

            }
            else
            {
                isDownloading = false;
                OnDownloadError?.Invoke();
            }
            request.Dispose();
        }
    }
}
