using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Donations;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.DecoratorBased;
using DCL.PlacesAPIService;
using DCL.SocialService;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.SceneLifeCycle;
using Global.AppArgs;
using System;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class SocialServicesContainer : IDisposable
    {
        private readonly IDecentralandUrlsSource dclUrlSource;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IAppArgs appArgs;

        internal readonly RPCSocialServices socialServicesRPC;

        private CancellationTokenSource cts = new ();

        public ISocialServiceEventBus EventBus { get; }

        public IDonationsService DonationsService { get; }

        public SocialServicesContainer(IDecentralandUrlsSource dclUrlSource,
            IWeb3IdentityCache web3IdentityCache,
            IAppArgs appArgs,
            IScenesCache scenesCache,
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IPlacesAPIService placesAPIService,
            DecentralandEnvironment environment,
            IAnalyticsController analyticsController,
            bool localSceneDevelopment,
            bool enableAnalytics)
        {
            this.dclUrlSource = dclUrlSource;
            this.web3IdentityCache = web3IdentityCache;
            this.appArgs = appArgs;

            EventBus = new SocialServiceEventBus();

            // We need to restart the connection to the service as identity changes
            // since that affects which friends the user can access
            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReInitializeRpcClient;

            socialServicesRPC = new RPCSocialServices(GetApiUrl(), web3IdentityCache, EventBus);

            DonationsService = CreateDonationsService(scenesCache, ethereumApi, webRequestController, realmData, placesAPIService,
                environment, dclUrlSource, localSceneDevelopment, analyticsController, enableAnalytics);
        }

        private static IDonationsService CreateDonationsService(
            IScenesCache scenesCache,
            IEthereumApi ethereumApi,
            IWebRequestController webRequestController,
            IRealmData realmData,
            IPlacesAPIService placesAPIService,
            DecentralandEnvironment environment,
            IDecentralandUrlsSource urlsSource,
            bool localSceneDevelopment,
            IAnalyticsController analyticsController,
            bool enableAnalytics)
        {
            if (!FeaturesRegistry.Instance.IsEnabled(FeatureId.DONATIONS))
                return new DonationsServiceDisabled();

            IDonationsService core = new DonationsService(scenesCache, ethereumApi, webRequestController, realmData,
                placesAPIService, environment, urlsSource, localSceneDevelopment);

            return enableAnalytics ? new DonationsServiceAnalyticsDecorator(core, analyticsController) : core;
        }

        public void Dispose()
        {
            DonationsService.Dispose();
            socialServicesRPC.Dispose();
            web3IdentityCache.OnIdentityCleared -= DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged -= ReInitializeRpcClient;
        }

        private void ReInitializeRpcClient()
        {
            cts = cts.SafeRestart();
            ReconnectRpcClientAsync(cts.Token).Forget();
            return;

            async UniTaskVoid ReconnectRpcClientAsync(CancellationToken ct)
            {
                try
                {
                    await socialServicesRPC.DisconnectAsync(ct);
                    await socialServicesRPC.EnsureRpcConnectionAsync(int.MaxValue, ct);
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { ReportHub.LogException(e, ReportCategory.ENGINE); }

                EventBus.SendTransportReconnectedNotification();
            }
        }

        private void DisconnectRpcClient()
        {
            cts = cts.SafeRestart();
            DisconnectRpcClientAsync(cts.Token).Forget();
            return;

            async UniTaskVoid DisconnectRpcClientAsync(CancellationToken ct)
            {
                await socialServicesRPC.DisconnectAsync(ct).SuppressToResultAsync(ReportCategory.ENGINE);
            }
        }

        private URLAddress GetApiUrl()
        {
            string url = dclUrlSource.Url(DecentralandUrl.ApiFriends);

            if (appArgs.TryGetValue(AppArgsFlags.FRIENDS_API_URL, out string? urlFromArgs))
                url = urlFromArgs!;

            return URLAddress.FromString(url);
        }
    }
}
