#nullable enable

using Cysharp.Threading.Tasks;
using DCL.WebRequests.WebContentSizes.Sizes;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.WebRequests.WebContentSizes
{
    public class ContentLengthBasedWebContentSizes : IWebContentSizes
    {
        private readonly IMaxSize maxSize;

        public ContentLengthBasedWebContentSizes(IMaxSize maxSize)
        {
            this.maxSize = maxSize;
        }

        public async UniTask<bool> IsOkSizeAsync(string url, CancellationToken cancellationToken)
        {
            var request = UnityWebRequest.Head(url)!;
            await request.SendWebRequest()!.WithCancellation(cancellationToken);

            if (request.isDone && TryGetLength(request, out ulong length))
                return length != 0 && length < maxSize.MaxSizeInBytes();

            return false;
        }

        private static bool TryGetLength(UnityWebRequest request, out ulong length) =>
            ulong.TryParse(request.GetResponseHeader("Content-Length") ?? "NONE", out length);
    }
}
