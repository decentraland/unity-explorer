#nullable enable

using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests.WebContentSizes.Sizes;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests.WebContentSizes
{
    public class ContentLengthBasedWebContentSizes : IWebContentSizes
    {
        private readonly IMaxSize maxSize;
        private readonly IWebRequestController webRequestController;

        public ContentLengthBasedWebContentSizes(IMaxSize maxSize, IWebRequestController webRequestController)
        {
            this.maxSize = maxSize;
            this.webRequestController = webRequestController;
        }

        public async UniTask<bool> IsOkSizeAsync(Uri url, CancellationToken cancellationToken)
        {
            string? header = await webRequestController.HeadAsync(url, ReportCategory.GENERIC_WEB_REQUEST)
                                                       .GetResponseHeaderAsync(WebRequestHeaders.CONTENT_LENGTH_HEADER, cancellationToken);

            return WebRequestHeaders.TryParseUnsigned(header, out ulong length) && length != 0 && length < maxSize.MaxSizeInBytes();
        }
    }
}
