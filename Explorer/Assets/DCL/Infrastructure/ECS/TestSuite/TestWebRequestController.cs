using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.DebugUtilities.UIBindings;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.RequestsHub;
using NSubstitute;

namespace ECS.TestSuite
{
    public class TestWebRequestController
    {
        private const int TOTAL_BUDGET = int.MaxValue;

        public static readonly IWebRequestController INSTANCE = new WebRequestController(
            Substitute.For<IWebRequestsAnalyticsContainer>(),
            Substitute.For<IWeb3IdentityCache>(),
            new RequestHub(Substitute.For<IDecentralandUrlsSource>()),
            ChromeDevtoolProtocolClient.NewForTest(),
            new WebRequestBudget(TOTAL_BUDGET,
                new ElementBinding<ulong>(TOTAL_BUDGET))
        );
    }
}
