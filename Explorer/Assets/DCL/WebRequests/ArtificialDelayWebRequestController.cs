using Cysharp.Threading.Tasks;
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
            await DelayAsync();
            return await origin.SendAsync(requestWrap, detachDownloadHandler, ct);
        }

        public async UniTask<PartialDownloadStream> GetPartialAsync(CommonArguments commonArguments, PartialDownloadArguments partialArgs, CancellationToken ct, WebRequestHeadersInfo? headersInfo = null)
        {
            await DelayAsync();
            return await origin.GetPartialAsync(commonArguments, partialArgs, ct, headersInfo);
        }

        private async Task DelayAsync()
        {
            (float delaySeconds, bool useDelay) = await options.GetOptionsAsync();

            if (useDelay)
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        public interface IReadOnlyOptions
        {
            UniTask<(float ArtificialDelaySeconds, bool UseDelay)> GetOptionsAsync();
        }
    }
}
