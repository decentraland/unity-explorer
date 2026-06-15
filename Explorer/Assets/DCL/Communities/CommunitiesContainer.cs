using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Global.AppArgs;
using MVC;
using System;
using System.Threading;

namespace DCL.Communities
{
    /// <summary>
    ///     Communities data access and feature gating.
    /// </summary>
    public class CommunitiesContainer : IDisposable
    {
        private CommunityDataService? dataService;

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

        /// <summary>
        ///     Deferred factory: <see cref="CommunityDataService" /> needs chat history and the MVC manager, which exist
        ///     only after their containers are built — later than this container, whose <see cref="DataProvider" /> is
        ///     required early by realm-navigation gating. Mirrors the <c>CreatePlugin</c> pattern.
        /// </summary>
        public CommunityDataService CreateDataService(IChatHistory chatHistory, IMVCManager mvcManager, IWeb3IdentityCache identityCache)
        {
            dataService = new CommunityDataService(chatHistory, mvcManager, EventBus, DataProvider, identityCache);
            return dataService;
        }

        public void Dispose() =>
            dataService?.Dispose();
    }
}
