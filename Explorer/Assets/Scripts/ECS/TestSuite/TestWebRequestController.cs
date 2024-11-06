#nullable enable

using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.RequestsHub;
using NSubstitute;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;

namespace ECS.TestSuite
{
    public class TestWebRequestController
    {
        public static readonly IWebRequestController INSTANCE = new WebRequestController(
            Substitute.For<IWebRequestsAnalyticsContainer>(),
            Substitute.For<IWeb3IdentityCache>(),
            new RequestHub(ITexturesFuse.NewTestInstance())
        );
    }
}
