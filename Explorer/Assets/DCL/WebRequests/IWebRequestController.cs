using Cysharp.Threading.Tasks;
using DCL.Browser.DecentralandUrls;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using Global.Dynamic.LaunchModes;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    public interface IWebRequestController
    {
        public static readonly ISet<long> IGNORE_NOT_FOUND = new HashSet<long> { WebRequestUtils.NOT_FOUND };

        public IRequestHub RequestHub { get; }

        public UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op)
            where TWebRequestArgs: struct
            where TWebRequest: struct, ITypedWebRequest
            where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>;
#if UNITY_EDITOR
        public static int TOTAL_BUDGET = 15;

        public static readonly IWebRequestController TEST = new WebRequestController(
            IWebRequestsAnalyticsContainer.TEST,
            new IWeb3IdentityCache.Default(),
            new RequestHub(
                new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY)
            ),
            ChromeDevtoolProtocolClient.NewForTest(),
            new WebRequestBudget(TOTAL_BUDGET,
                new ElementBinding<ulong>((ulong)TOTAL_BUDGET))
        );
#endif
    }
}
