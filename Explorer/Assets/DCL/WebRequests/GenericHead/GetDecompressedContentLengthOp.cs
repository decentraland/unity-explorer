using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.WebRequests
{
    public readonly struct GetDecompressedContentLengthOp : IWebRequestOp<GenericHeadRequest, long>
    {
        private const string DECOMPRESSED_CONTENT_LENGTH_HEADER = "x-decompressed-content-length";

        public UniTask<long> ExecuteAsync(GenericHeadRequest webRequest, CancellationToken ct)
        {
            string header = webRequest.UnityWebRequest.GetResponseHeader(DECOMPRESSED_CONTENT_LENGTH_HEADER);

            if (header != null && long.TryParse(header, out long contentLength))
                return UniTask.FromResult(contentLength);

            return UniTask.FromResult(-1L);
        }
    }
}