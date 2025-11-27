#nullable enable

using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private static readonly int TOTAL_BUDGET = 15;


        public static readonly IWebRequestController INSTANCE = new WebRequestController(
            Substitute.For<IWebRequestsAnalyticsContainer>(),
            Substitute.For<IWeb3IdentityCache>(),
            new RequestHub(Substitute.For<IDecentralandUrlsSource>()),
            ChromeDevtoolProtocolClient.NewForTest(),
            new ElementBinding<ulong>((ulong)TOTAL_BUDGET),
            TOTAL_BUDGET
        );
    }
}
