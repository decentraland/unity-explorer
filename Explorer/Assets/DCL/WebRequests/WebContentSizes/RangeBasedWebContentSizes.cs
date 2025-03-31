using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.WebContentSizes.Sizes;
using System.Linq;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests.WebContentSizes
{
    // TODO жуть
    public class RangeBasedWebContentSizes : IWebContentSizes
    {
        private readonly IMaxSize maxSize;
        private const ulong TRESS_HOLD_BYTES = 2048;
        private const ulong UPPER_LIMIT = TRESS_HOLD_BYTES * 2;

        private readonly IWebRequestController webRequestController;

        public RangeBasedWebContentSizes(IMaxSize maxSize, IWebRequestController webRequestController)
        {
            this.maxSize = maxSize;
            this.webRequestController = webRequestController;
        }

        public async UniTask<bool> IsOkSizeAsync(string url, CancellationToken token)
        {
            using var request = NewRequest(url);
            request.SendWebRequest().WithCancellation(token);

            while (token.IsCancellationRequested == false
                   && request.isDone == false)
            {
                if (request.downloadedBytes > UPPER_LIMIT)
                {
                    ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, $"Seems the remote server doesn't support Range Header, Downloaded bytes {request.downloadedBytes} exceeded upper limit {UPPER_LIMIT}, aborting request");
                    ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, ReadableResponseHeaders(request));
                    request.Abort();
                    return false;
                }

                await UniTask.Yield();
            }

            if (IsResultError(request))
            {
                ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, $"Error while checking size of {url}: {request.error}");
                return false;
            }

            if (IsDownloadedTooMuch(request))
            {
                ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, $"Size of {url} is {request.downloadedBytes} bytes, which is greater than the tress hold of {TRESS_HOLD_BYTES} bytes");
                return false;
            }

            return true;
        }

        private GenericGetRequest NewRequest(string url)
        {
            ulong max = maxSize.MaxSizeInBytes();
            ulong tressHold = max + TRESS_HOLD_BYTES;
            GenericGetRequest request = webRequestController.GetAsync(url, ReportCategory.GENERIC_WEB_REQUEST, new WebRequestHeadersInfo().WithRange((long)max, (long)tressHold))
            return request;
        }

        private static bool IsDownloadedTooMuch(IWebRequest request) =>
            request.downloadedBytes > TRESS_HOLD_BYTES;

        private static bool IsResultError(IWebRequest request) =>
            request.result
                is UnityWebRequest.Result.ConnectionError
                or UnityWebRequest.Result.ProtocolError
                or UnityWebRequest.Result.DataProcessingError;

        private static string ReadableResponseHeaders(UnityWebRequest request)
        {
            var list = request.GetResponseHeaders()!.Select(e => $"{e.Key}: {e.Value}");
            return $"Headers of response:\n{string.Join('\n', list)}";
        }
    }
}
