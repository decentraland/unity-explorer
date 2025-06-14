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
        private readonly ISocialServiceEventBus socialServiceEventBus;
        private readonly IAppArgs appArgs;

        internal readonly RPCSocialServices socialServicesRPC;

        private CancellationTokenSource cts = new ();

        public SocialServicesContainer(IDecentralandUrlsSource dclUrlSource,
            IWeb3IdentityCache web3IdentityCache,
            ISocialServiceEventBus socialServiceEventBus,
            IAppArgs appArgs)
        {
            this.dclUrlSource = dclUrlSource;
            this.web3IdentityCache = web3IdentityCache;
            this.socialServiceEventBus = socialServiceEventBus;
            this.appArgs = appArgs;

            // We need to restart the connection to the service as identity changes
            // since that affects which friends the user can access
            web3IdentityCache.OnIdentityCleared += DisconnectRpcClient;
            web3IdentityCache.OnIdentityChanged += ReInitializeRpcClient;

            socialServicesRPC = new RPCSocialServices(GetApiUrl(), web3IdentityCache, socialServiceEventBus);
        }

        public void Dispose()
        {
            socialServicesRPC?.Dispose();
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

                socialServiceEventBus.SendTransportReconnectedNotification();
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

        private Uri GetApiUrl()
        {
            Uri url = dclUrlSource.Url(DecentralandUrl.ApiFriends);

            if (appArgs.TryGetValue(AppArgsFlags.FRIENDS_API_URL, out string? urlFromArgs))
                url = new Uri(urlFromArgs!);

            return url;
        }
    }
}
