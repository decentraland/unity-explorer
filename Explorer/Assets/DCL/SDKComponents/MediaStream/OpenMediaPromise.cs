using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.SDKComponents.MediaStream
{
    public class OpenMediaPromise
    {
        internal enum Status
        {
            Pending, Resolved, Consumed,
        }

        internal Status status;

        // TODO (Vit): add caching mechanism for resolved promises: <url, isReachable> (on some upper level)
        internal string url;
        internal bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public async UniTask UrlReachabilityResolveAsync(IWebRequestController webRequestController, string url, ReportData reportData, CancellationToken ct)
        {
            status = Status.Pending;
            isReachable = false;
            this.url = url;

            isReachable = await webRequestController.IsHeadReachableAsync(reportData, URLAddress.FromString(this.url), ct, 5);
            //This is needed because some servers might not handle HEAD requests correctly and return 404 errors, even thou they are perfectly
            if (!isReachable)
                isReachable = await IsGetReachableAsync(ct);

            ReportHub.Log(ReportCategory.MEDIA_STREAM, $"Resource <{url}> isReachable = <{isReachable}>");

            status = Status.Resolved;
        }

        /*The following function is a temporary workardound for Issue #2485
         *The streaming server was taking 3 minutes to fail with timeout in the IsHeadReachableAsync method, by adding a timeout it was getting stuck
         *in the previous IsGetReachable async that was performing a get request, starting the stream and never ending
         *There was no other way other then the new UnityWebRequest to start the request with the webrequestcontroller and interrupt it after a few bytes were received
         * This will be soon replaced by the integration of HTTP2 library that will provide a clearer way to solve the problem
         */
        private async UniTask<bool> IsGetReachableAsync(CancellationToken ct)
        {
            UnityWebRequest isGetReachableRequest = UnityWebRequest.Get(this.url);
            isGetReachableRequest.SendWebRequest().ToUniTask(cancellationToken: ct);

            while (isGetReachableRequest.downloadedBytes == 0)
            {
                if (!string.IsNullOrEmpty(isGetReachableRequest.error))
                    return false;

                await UniTask.Yield();
            }

            isGetReachableRequest.Abort();
            return true;
        }

        public bool IsReachableConsume(string url)
        {
            status = Status.Consumed;

            if (this.url != url)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume different url - wanted <{url}>, but was <{this.url}>");
                return false;
            }

            if (!isReachable)
            {
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Try to consume not reachable URL <{this.url}>");
                return false;
            }

            return true;
        }
    }
}
