using Cysharp.Threading.Tasks;
using DCL.WebRequests.WebContentSizes.Sizes;
using System.Threading;

namespace DCL.WebRequests.WebContentSizes
{
    public interface IWebContentSizes
    {
        UniTask<bool> IsOkSizeAsync(string url, CancellationToken cancellationToken);

        class Default : IWebContentSizes
        {
            private readonly IWebContentSizes webContentSizes;

            public Default(IMaxSize maxSize)
            {
                webContentSizes = new SeveralWebContentSizes(
                    new ContentLengthBasedWebContentSizes(maxSize),
                    new RangeBasedWebContentSizes(maxSize)
                );
            }

            public UniTask<bool> IsOkSizeAsync(string url, CancellationToken cancellationToken) =>
                webContentSizes.IsOkSizeAsync(url, cancellationToken);
        }
    }
}
