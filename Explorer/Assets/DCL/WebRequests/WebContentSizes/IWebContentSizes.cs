using Cysharp.Threading.Tasks;
using DCL.WebRequests.WebContentSizes.Sizes;
using System;
using System.Threading;

namespace DCL.WebRequests.WebContentSizes
{
    public interface IWebContentSizes
    {
        UniTask<bool> IsOkSizeAsync(Uri url, CancellationToken cancellationToken);

        class Default : IWebContentSizes
        {
            private readonly IWebContentSizes webContentSizes;

            public Default(IMaxSize maxSize, IWebRequestController webRequestController)
            {
                webContentSizes = new SeveralWebContentSizes(
                    new ContentLengthBasedWebContentSizes(maxSize, webRequestController),
                    new RangeBasedWebContentSizes(maxSize, webRequestController)
                );
            }

            public UniTask<bool> IsOkSizeAsync(Uri url, CancellationToken cancellationToken) =>
                webContentSizes.IsOkSizeAsync(url, cancellationToken);
        }
    }
}
