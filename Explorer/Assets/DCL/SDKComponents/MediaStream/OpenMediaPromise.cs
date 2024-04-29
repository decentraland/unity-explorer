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
        private enum Status {
            Pending, Resolved, Consumed,
        }

        private Status status;

        private readonly Action<MediaPlayer> onResolved;

        private string url;
        private bool isReachable;

        public async UniTask CheckIfReachableAsync(IWebRequestController webRequestController, string url, CancellationToken ct)
        {

            status = Status.Pending;

            this.url = url;
            isReachable = await webRequestController.IsReachableAsync(URLAddress.FromString(this.url), ct);

            status = Status.Resolved;
        }

        public bool CanConsume(string url) =>
            status == Status.Resolved && isReachable && this.url == url;

        public void Consume(MediaPlayer mediaPlayer, bool autoPlay)
        {
            status = Status.Consumed;

            mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay);

            isReachable = false;
        }
    }
}
