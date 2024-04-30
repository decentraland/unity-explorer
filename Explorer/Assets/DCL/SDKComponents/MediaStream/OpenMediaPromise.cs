using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using RenderHeads.Media.AVProVideo;
using System;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public class OpenMediaPromise
    {
        private enum Status
        {
            Pending, Resolved, Consumed,
        }

        private Status status;

        // TODO (Vit): add caching mechanism for resolved promises: <url, isReachable> (on some upper level)
        private string url;
        private bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public async UniTask UrlReachabilityResolveAsync(IWebRequestController webRequestController, string url, CancellationToken ct)
        {
            status = Status.Pending;
            isReachable = false;
            this.url = url;

            isReachable = await webRequestController.IsReachableAsync(URLAddress.FromString(this.url), ct);

            status = Status.Resolved;
        }

        public bool IsReachableConsume(string url)
        {
            status = Status.Consumed;
            return isReachable && this.url == url;
        }
    }
}
