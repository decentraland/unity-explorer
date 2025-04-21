using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.WebContentSizes.Sizes;
using System.Threading;

namespace DCL.WebRequests.WebContentSizes
{
    public class RangeBasedWebContentSizes : IWebContentSizes
    {
        private const long TEST_THRESHOLD = 200;

        private readonly IMaxSize maxSize;
        private readonly IWebRequestController webRequestController;

        public RangeBasedWebContentSizes(IMaxSize maxSize, IWebRequestController webRequestController)
        {
            this.maxSize = maxSize;
            this.webRequestController = webRequestController;
        }

        public async UniTask<bool> IsOkSizeAsync(string url, CancellationToken token)
        {
            // Make a Head request with Range

            WebRequestHeadersInfo reqHeader = new WebRequestHeadersInfo().WithRange(0, TEST_THRESHOLD);

            string? header = await webRequestController.HeadAsync(url, ReportCategory.GENERIC_WEB_REQUEST, reqHeader)
                                                       .GetResponseHeaderAsync(WebRequestHeaders.CONTENT_RANGE_HEADER, token);

            return DownloadHandlersUtils.TryParseContentRange(header, out long fullSize, out _) && (ulong)fullSize < maxSize.MaxSizeInBytes();
        }
    }
}
