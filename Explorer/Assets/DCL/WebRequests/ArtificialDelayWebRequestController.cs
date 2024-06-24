using Cysharp.Threading.Tasks;
using System;

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

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            RequestEnvelope<TWebRequest, TWebRequestArgs> envelope,
            TWebRequestOp op
        )
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            (float delaySeconds, bool useDelay) = await options.GetOptionsAsync();

            if (useDelay)
                await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));

            return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
        }

        public interface IReadOnlyOptions
        {
            UniTask<(float ArtificialDelaySeconds, bool UseDelay)> GetOptionsAsync();
        }
    }
}
