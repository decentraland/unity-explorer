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
        private readonly IWebRequestController webRequestController;
        private readonly string url;

        public readonly Action<MediaPlayer> OnResolved;

        public bool IsReachable { get; private set; }
        public bool IsResolved { get; private set; }

        public static OpenMediaPromise Create(IWebRequestController webRequestController, string url, Action<MediaPlayer> onResolved, CancellationToken ct)
        {
            var promise = new OpenMediaPromise(webRequestController, url, onResolved);
            promise.CheckIfReachableAsync(ct).Forget();

            return promise;
        }

        public void Reject()
        {

        }

        private OpenMediaPromise(IWebRequestController webRequestController,  string url, Action<MediaPlayer> onResolved)
        {
            this.webRequestController = webRequestController;
            this.url = url;

            this.OnResolved = onResolved;
        }

        private async UniTask CheckIfReachableAsync(CancellationToken ct)
        {
            IsReachable = await webRequestController.IsReachableAsync(URLAddress.FromString(url), ct);
            IsResolved = true;
        }

    }
}
