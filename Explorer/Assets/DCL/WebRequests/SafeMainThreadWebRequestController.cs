using Cysharp.Threading.Tasks;
using Utility.Multithreading;

namespace DCL.WebRequests
{
    public class SafeMainThreadWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;

        public SafeMainThreadWebRequestController(IWebRequestController origin)
        {
            this.origin = origin;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op) where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            await using var scope = await ExecuteOnMainThreadScope.NewScopeWithReturnOnOriginalThreadAsync();
            MultithreadingUtility.AssertMainThread(nameof(SendAsync), true);
            var result = await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            return result;
        }
    }
}
