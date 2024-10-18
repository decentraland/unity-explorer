#nullable enable

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.WebContentSizes.Sizes;
using System;
using System.Linq;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests.WebContentSizes
{
    public class RangeBasedWebContentSizes : IWebContentSizes
    {
        private readonly IMaxSize maxSize;
        private readonly Action<string> reasonLog;
        private const ulong TRESS_HOLD_BYTES = 2048;
        private const ulong UPPER_LIMIT = TRESS_HOLD_BYTES * 2;

        public RangeBasedWebContentSizes(IMaxSize maxSize) : this(
            maxSize,
            s => ReportHub.LogWarning(ReportCategory.GENERIC_WEB_REQUEST, s)
        ) { }

        public RangeBasedWebContentSizes(IMaxSize maxSize, Action<string> reasonLog)
        {
            this.maxSize = maxSize;
            this.reasonLog = reasonLog;
        }

        public async UniTask<bool> IsOkSizeAsync(string url, CancellationToken token)
        {
            var request = NewRequest(url);
            request.SendWebRequest().WithCancellation(token);

            while (token.IsCancellationRequested == false
                   && request.isDone == false)
            {
                if (request.downloadedBytes > UPPER_LIMIT)
                {
                    reasonLog($"Seems the remote server doesn't support Range Header, Downloaded bytes {request.downloadedBytes} exceeded upper limit {UPPER_LIMIT}, aborting request");
                    reasonLog(ReadableResponseHeaders(request));
                    request.Abort();
                    request.Dispose();
                    return false;
                }

                await UniTask.Yield();
            }

            if (IsResultError(request))
            {
                reasonLog($"Error while checking size of {url}: {request.error}");
                return false;
            }

            if (IsDownloadedTooMuch(request))
            {
                reasonLog($"Size of {url} is {request.downloadedBytes} bytes, which is greater than the tress hold of {TRESS_HOLD_BYTES} bytes");
                return false;
            }

            return true;
        }

        private UnityWebRequest NewRequest(string url)
        {
            var request = UnityWebRequest.Get(url)!;
            ulong max = maxSize.MaxSizeInBytes();
            ulong tressHold = max + TRESS_HOLD_BYTES;
            request.SetRequestHeader("Range", $"bytes:{max}-{tressHold}");
            request.SetRequestHeader("Accept-Range", $"bytes:{max}-{tressHold}");
            return request;
        }

        private static bool IsDownloadedTooMuch(UnityWebRequest request) =>
            request.downloadedBytes > TRESS_HOLD_BYTES;

        private static bool IsResultError(UnityWebRequest request) =>
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
