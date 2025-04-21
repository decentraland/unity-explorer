using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.RequestsHub;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.WebRequests
{
    public class ArtificialDelayWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly IReadOnlyOptions options;

        public ArtificialDelayWebRequestController(IWebRequestController origin, IReadOnlyOptions options)
        {
            this.origin = origin;
            this.options = options;
        }

        IRequestHub IWebRequestController.requestHub => origin.requestHub;

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            await DelayAsync(ct);
            return await origin.SendAsync(requestWrap, detachDownloadHandler, ct);
        }

        private async Task DelayAsync(CancellationToken ct)
        {
            (float delaySeconds, bool useDelay) = await options.GetOptionsAsync();

            if (useDelay)
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken: ct);
        }

        public interface IReadOnlyOptions
        {
            UniTask<(float ArtificialDelaySeconds, bool UseDelay)> GetOptionsAsync();
        }
    }
}
