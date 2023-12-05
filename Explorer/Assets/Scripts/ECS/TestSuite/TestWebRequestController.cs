using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using NSubstitute;

namespace ECS.TestSuite
{
    public class TestWebRequestController
    {
        public static readonly IWebRequestController INSTANCE = new WebRequestController(Substitute.For<IWebRequestsAnalyticsContainer>());
    }
}
