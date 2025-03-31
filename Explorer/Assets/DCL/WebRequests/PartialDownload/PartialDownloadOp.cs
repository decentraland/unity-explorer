using Cysharp.Threading.Tasks;
using DCL.WebRequests.CustomDownloadHandlers;
using System;
using System.Threading;

namespace DCL.WebRequests
{
    public static partial class WebRequestControllerExtensions
    {
        internal struct PartialDownloadOp : IWebRequestOp<PartialDownloadRequest, PartialDownloadedData>
        {
            public UniTask<PartialDownloadedData> ExecuteAsync(PartialDownloadRequest webRequest, CancellationToken ct)
            {
                var partialDownloadHandler = (PartialDownloadHandler)webRequest.UnityWebRequest.downloadHandler;
                int fullFileSize;

                if (DownloadHandlersUtils.TryGetFullSize(webRequest.UnityWebRequest.GetResponseHeader(CONTENT_RANGE_HEADER), out int fullSize)) { fullFileSize = fullSize; }
                else if (int.TryParse(webRequest.UnityWebRequest.GetResponseHeader(CONTENT_LENGTH_HEADER), out int contentSize)) { fullFileSize = contentSize; }
                else { fullFileSize = Convert.ToInt32(webRequest.UnityWebRequest.downloadedBytes); }

                return UniTask.FromResult(new PartialDownloadedData(partialDownloadHandler.PartialData, partialDownloadHandler.DownloadedSize, fullFileSize));
            }
        }
    }
}
