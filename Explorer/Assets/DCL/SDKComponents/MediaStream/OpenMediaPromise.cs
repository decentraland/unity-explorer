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

        private readonly Action<MediaPlayer> onResolved;

        private Status status;

        private string url;
        private bool isReachable;

        public bool IsResolved => status == Status.Resolved;

        public async UniTask CheckIfReachableAsync(IWebRequestController webRequestController, string url, CancellationToken ct)
        {
            status = Status.Pending;
            isReachable = false;
            this.url = url;

            isReachable = await webRequestController.IsReachableAsync(URLAddress.FromString(this.url), ct);

            status = Status.Resolved;
        }

        public bool TryConsume(MediaPlayer mediaPlayer, string url, bool autoPlay)
        {
            if (isReachable && this.url == url)
            {
                status = Status.Consumed;
                mediaPlayer.OpenMedia(MediaPathType.AbsolutePathOrURL, url, autoPlay);
                return true;
            }

            return false;
        }
    }
}
