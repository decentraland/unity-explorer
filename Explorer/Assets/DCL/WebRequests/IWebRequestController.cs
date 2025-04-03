using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        static readonly IWebRequestController DEFAULT = new WebRequestController(
            IWebRequestsAnalyticsContainer.DEFAULT,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                true
            )
        );

        public static readonly ISet<long> IGNORE_NOT_FOUND = new HashSet<long> { WebRequestUtils.NOT_FOUND };

        UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>;

        internal IRequestHub requestHub { get; }
    }
}
