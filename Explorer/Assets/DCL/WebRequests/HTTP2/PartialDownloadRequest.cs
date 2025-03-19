using Best.HTTP;
using Best.HTTP.Caching;
using Best.HTTP.Response;
using DCL.WebRequests.CustomDownloadHandlers;
using System;
using System.Collections.Generic;
using System.Threading;
using static DCL.WebRequests.WebRequestControllerExtensions;

namespace DCL.WebRequests.HTTP2
{
    /// <summary>
    ///     TODO: This shite should be pooled
    /// </summary>
    public class PartialDownloadRequest
    {
        private byte[]? partialData;
        private int fullFileSize;

        public void OnRequestCreated(HTTPRequest request)
        {
            // Make sure the downloading buffer is aligned with the chunk size so we don't have to deal with partial chunks of data (which is already partial)
            request.DownloadSettings.ContentStreamMaxBuffered = PartialDownloadingRange.CHUNK_SIZE;

            // There is an opportunity to modify settings before starting the request

            request.DownloadSettings.OnHeadersReceived += OnHeadersReceived;
            request.DownloadSettings.OnDownloadStarted += OnDownloadStarted;
        }

        private void OnDownloadStarted(HTTPRequest req, HTTPResponse resp, DownloadContentStream stream) { }

        private void OnHeadersReceived(HTTPRequest req, HTTPResponse resp, Dictionary<string, List<string>> headers)
        {
            // We need to know how many bytes to allocate for a buffer

            if (DownloadHandlersUtils.TryGetFullSize(resp.GetFirstHeaderValue(CONTENT_RANGE_HEADER), out int fullSize))
                fullFileSize = fullSize;
            else if (int.TryParse(resp.GetFirstHeaderValue(CONTENT_LENGTH_HEADER), out int contentSize))
                fullFileSize = contentSize;
            else
                fullFileSize = Convert.ToInt32(webRequest.UnityWebRequest.downloadedBytes);
        }

        public void OnRequestFinished(HTTPRequest request)
        {
            request.DownloadSettings.OnHeadersReceived -= OnHeadersReceived;
            request.DownloadSettings.OnDownloadStarted -= OnDownloadStarted;
        }
    }
}
