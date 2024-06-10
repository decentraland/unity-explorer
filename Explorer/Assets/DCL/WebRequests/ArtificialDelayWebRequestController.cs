using Cysharp.Threading.Tasks;
using System;

namespace DCL.WebRequests
{
    public class ArtificialDelayWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly TimeSpan delay;

        public ArtificialDelayWebRequestController(IWebRequestController origin, float delaySeconds) : this(
            origin,
            TimeSpan.FromSeconds(delaySeconds)
        ) { }

        public ArtificialDelayWebRequestController(IWebRequestController origin, TimeSpan delay)
        {
            this.origin = origin;
            this.delay = delay;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(
            RequestEnvelope<TWebRequest, TWebRequestArgs> envelope,
            TWebRequestOp op
        )
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestArgs: struct
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            var result = await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            await UniTask.Delay(delay);
            return result;
        }
    }
}
