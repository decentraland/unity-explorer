using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.SocialService;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
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

        public SocialServicesContainer(IDecentralandUrlsSource dclUrlSource,
            IWeb3IdentityCache web3IdentityCache,
            IAppArgs appArgs)
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
        }

        public void Dispose()
        {
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
