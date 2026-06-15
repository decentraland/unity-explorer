using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Global.AppArgs;
using System.Threading;

namespace DCL.Communities
{
    /// <summary>
    ///     Communities data access and feature gating.
    /// </summary>
    public class CommunitiesContainer
    {
        public CommunitiesDataProvider.CommunitiesDataProvider DataProvider { get; }

        public CommunitiesEventBus EventBus { get; }

        public bool IncludeCommunities { get; }

        private CommunitiesContainer(CommunitiesDataProvider.CommunitiesDataProvider dataProvider, CommunitiesEventBus eventBus, bool includeCommunities)
        {
            DataProvider = dataProvider;
            EventBus = eventBus;
            IncludeCommunities = includeCommunities;
        }

        public static async UniTask<CommunitiesContainer> CreateAsync(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache identityCache,
            IAppArgs appArgs,
            CancellationToken ct)
        {
            var dataProvider = new CommunitiesDataProvider.CommunitiesDataProvider(webRequestController, urlsSource, identityCache);

            CommunitiesFeatureAccess.Initialize(new CommunitiesFeatureAccess(identityCache, appArgs));
            bool includeCommunities = await CommunitiesFeatureAccess.Instance.IsUserAllowedToUseTheFeatureAsync(ct, ignoreAllowedList: true, cacheResult: false);

            return new CommunitiesContainer(dataProvider, new CommunitiesEventBus(), includeCommunities);
        }
    }
}
